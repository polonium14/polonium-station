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
/// Two wounds that end up at the same severity must bleed the same, whether they got there in
/// one hit or several. They used not to: a wound born at or above BleedInflicter's
/// severityThreshold accrues from its whole severity in OnWoundAdded, while one that grows
/// across the threshold went through OnBleedInflicterSeverityUpdate, which only ever credited
/// the delta - so the sub-threshold portion of the wound never turned into bleeding and
/// spreading the same damage over more hits produced less blood.
///
/// ScalingLimit diverged the same way in the opposite direction: the growth path took a
/// +0.6 bump meant for reopening a stopped bleed, which OnWoundAdded never applies.
/// </summary>
[TestFixture]
[TestOf(typeof(SharedBloodstreamSystem))]
public sealed class BleedThresholdCrossingParityTest : GameTest
{
    // Piercing bleeds from severity 9, so 6 leaves the wound under the threshold and 12 clears it.
    private static readonly ProtoId<DamageTypePrototype> PiercingDamageType = "Piercing";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: BleedParityTestMob
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
  id: BleedParityTestArm
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
    public async Task SameSeverityBleedsTheSameWhetherReachedInOneHitOrTwo()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sSystems = server.ResolveDependency<IEntitySystemManager>();
        var sDamageable = sSystems.GetEntitySystem<DamageableSystem>();
        var sWound = sSystems.GetEntitySystem<WoundSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();

        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid oneHitMob = default, oneHitArm = default;
        EntityUid twoHitMob = default, twoHitArm = default;

        await server.WaitPost(() =>
        {
            var container = sEntMan.System<SharedContainerSystem>();

            oneHitMob = sEntMan.SpawnEntity("BleedParityTestMob", coords);
            oneHitArm = sEntMan.SpawnEntity("BleedParityTestArm", coords);
            container.Insert(oneHitArm, container.GetContainer(oneHitMob, BodyComponent.ContainerID));

            twoHitMob = sEntMan.SpawnEntity("BleedParityTestMob", coords);
            twoHitArm = sEntMan.SpawnEntity("BleedParityTestArm", coords);
            container.Insert(twoHitArm, container.GetContainer(twoHitMob, BodyComponent.ContainerID));
        });

        await pair.RunTicksSync(5);

        // One arm takes 12 in a single hit - born straight above the threshold.
        // The other takes 6 then 6 - the first leaves it under the threshold, the second
        // carries it across. Both end at severity 12.
        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(PiercingDamageType);
            sDamageable.TryChangeDamage(oneHitArm, new DamageSpecifier(proto, FixedPoint2.New(12)), ignoreResistances: true);
            sDamageable.TryChangeDamage(twoHitArm, new DamageSpecifier(proto, FixedPoint2.New(6)), ignoreResistances: true);
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(PiercingDamageType);
            sDamageable.TryChangeDamage(twoHitArm, new DamageSpecifier(proto, FixedPoint2.New(6)), ignoreResistances: true);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var (oneHitSeverity, oneHitBleeds) = ReadWound(sEntMan, sWound, oneHitArm);
            var (twoHitSeverity, twoHitBleeds) = ReadWound(sEntMan, sWound, twoHitArm);

            Assert.Multiple(() =>
            {
                Assert.That(twoHitSeverity, Is.EqualTo(oneHitSeverity),
                    "Setup: both arms must end at the same wound severity or there's nothing to compare.");
                Assert.That(oneHitBleeds.IsBleeding, Is.True,
                    "Setup: a single 12-severity Piercing wound is above the threshold and should bleed.");
                Assert.That(twoHitBleeds.IsBleeding, Is.True,
                    "Setup: growing to 12 should have carried the wound across the bleed threshold.");
            });

            Assert.Multiple(() =>
            {
                Assert.That(twoHitBleeds.BleedingAmountRaw, Is.EqualTo(oneHitBleeds.BleedingAmountRaw),
                    "Same severity, same bleed: the sub-threshold portion of a wound that grew into bleeding has to count too, "
                    + "otherwise spreading identical damage over more hits produces less blood than landing it at once.");

                Assert.That(twoHitBleeds.ScalingLimit, Is.EqualTo(oneHitBleeds.ScalingLimit),
                    "The +0.6 scaling bump is for reopening a bleed that was stopped, not for a wound crossing the "
                    + "threshold for the first time - otherwise the two paths still scale to different bleed rates.");
            });
        });
    }

    private static (FixedPoint2 Severity, BleedInflicterComponent Bleeds) ReadWound(
        IEntityManager sEntMan,
        WoundSystem sWound,
        EntityUid woundable)
    {
        var comp = sEntMan.GetComponent<WoundableComponent>(woundable);
        var wound = sWound.GetWoundableWounds(woundable, comp).First();

        return (sEntMan.GetComponent<WoundComponent>(wound).WoundSeverityPoint,
            sEntMan.GetComponent<BleedInflicterComponent>(wound));
    }
}
