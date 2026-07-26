// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

/// <summary>
/// A tourniquet suppresses bleeding while it's on, it doesn't make the wound underneath smaller.
/// The accrual handlers used to bail out on CanWoundBleed, so damage taken under a clamped limb
/// never turned into BleedingAmountRaw at all - take the tourniquet off and a badly mangled limb
/// bled like whatever single hit happened to land after removal.
///
/// The bleed is held off by IsBleeding instead: UpdateWounds re-derives it from CanWoundBleed
/// every tick and RecomputeWoundableBleeds only sums wounds that are actually bleeding, so
/// nothing leaks out while the clamp is on.
/// </summary>
[TestFixture]
[TestOf(typeof(SharedBloodstreamSystem))]
public sealed class TourniquetedWoundAccrualTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> PiercingDamageType = "Piercing";

    private const string ModifierId = "TourniquetPresent";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TourniquetAccrualTestMob
  components:
  - type: Body
  - type: Damageable
  - type: Injurable
  - type: Bloodstream
    bloodlossDamage:
      types:
        Bloodloss: 0.5
    bloodlossHealDamage:
      types:
        Bloodloss: -1

- type: entity
  id: TourniquetAccrualTestArm
  components:
  - type: Organ
    category: ArmLeft
  - type: Damageable
  - type: Injurable
  - type: Woundable
    integrityCap: 100
    thresholds:
      Healthy: 100
      Minor: 80
      Moderate: 60
      Severe: 40
      Critical: 20
      Mangled: 8
      Severed: 0
";

    [Test]
    public async Task DamageTakenUnderATourniquetStillCountsOnceItComesOff()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sSystems = server.ResolveDependency<IEntitySystemManager>();
        var sDamageable = sSystems.GetEntitySystem<DamageableSystem>();
        var sWound = sSystems.GetEntitySystem<WoundSystem>();
        var sBloodstream = sSystems.GetEntitySystem<SharedBloodstreamSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();

        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid clampedMob = default, clampedArm = default;
        EntityUid openMob = default, openArm = default;

        await server.WaitPost(() =>
        {
            var container = sEntMan.System<SharedContainerSystem>();

            clampedMob = sEntMan.SpawnEntity("TourniquetAccrualTestMob", coords);
            clampedArm = sEntMan.SpawnEntity("TourniquetAccrualTestArm", coords);
            container.Insert(clampedArm, container.GetContainer(clampedMob, BodyComponent.ContainerID));

            openMob = sEntMan.SpawnEntity("TourniquetAccrualTestMob", coords);
            openArm = sEntMan.SpawnEntity("TourniquetAccrualTestArm", coords);
            container.Insert(openArm, container.GetContainer(openMob, BodyComponent.ContainerID));
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(PiercingDamageType);
            sDamageable.TryChangeDamage(clampedArm, new DamageSpecifier(proto, FixedPoint2.New(12)), ignoreResistances: true);
            sDamageable.TryChangeDamage(openArm, new DamageSpecifier(proto, FixedPoint2.New(12)), ignoreResistances: true);
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            sBloodstream.TryAddBleedModifier(clampedArm, ModifierId, 100, canBleed: false, force: true);
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(PiercingDamageType);
            sDamageable.TryChangeDamage(clampedArm, new DamageSpecifier(proto, FixedPoint2.New(18)), ignoreResistances: true);
            sDamageable.TryChangeDamage(openArm, new DamageSpecifier(proto, FixedPoint2.New(18)), ignoreResistances: true);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var clamped = ReadBleeds(sEntMan, sWound, clampedArm);

            Assert.Multiple(() =>
            {
        // Another 18 on both - the clamped one takes it with bleeding suppressed.
                Assert.That(clamped.IsBleeding, Is.False,
                    "A tourniqueted limb must not be bleeding while the clamp is on.");
                Assert.That(sEntMan.GetComponent<WoundableComponent>(clampedArm).Bleeds, Is.EqualTo(FixedPoint2.Zero),
                    "Nothing should reach the bloodstream from a clamped limb, however much the wound accrued underneath.");
            });
        });

        await server.WaitPost(() =>
        {
            sBloodstream.TryRemoveBleedModifier(clampedArm, ModifierId, force: true);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var clamped = ReadBleeds(sEntMan, sWound, clampedArm);
            var open = ReadBleeds(sEntMan, sWound, openArm);

            Assert.Multiple(() =>
            {
                Assert.That(clamped.IsBleeding, Is.True,
                    "Taking the tourniquet off an unhealed wound should let it bleed again.");

                Assert.That(clamped.BleedingAmountRaw, Is.EqualTo(open.BleedingAmountRaw),
                    "The 18 taken under the tourniquet has to have accrued: a clamped limb ends up with the same "
                    + "wound as an unclamped one that took identical damage, so it must bleed the same once released.");

                Assert.That(clamped.ScalingLimit, Is.EqualTo(open.ScalingLimit),
                    "Growing while clamped reads as 'not bleeding' every tick - it must not be mistaken for repeated "
                    + "reopenings, or each hit stacks another +0.6 onto the scaling ceiling.");
            });
        });
    }

    private static BleedInflicterComponent ReadBleeds(IEntityManager sEntMan, WoundSystem sWound, EntityUid woundable)
    {
        var comp = sEntMan.GetComponent<WoundableComponent>(woundable);
        var wound = sWound.GetWoundableWounds(woundable, comp).First();

        return sEntMan.GetComponent<BleedInflicterComponent>(wound);
    }
}
