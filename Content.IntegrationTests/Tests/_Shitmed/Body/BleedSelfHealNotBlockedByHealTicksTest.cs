// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body;
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

[TestFixture]
[TestOf(typeof(Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems.WoundSystem))]
public sealed class BleedSelfHealNotBlockedByHealTicksTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> BluntDamageType = "Blunt";
    private static readonly ProtoId<DamageTypePrototype> AsphyxiationDamageType = "Asphyxiation";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: BleedHealTickTestAttacker
  components:
  - type: Targeting

- type: entity
  id: BleedHealTickTestVictim
  components:
  - type: Body
  - type: Damageable
  - type: Injurable

- type: entity
  id: BleedHealTickTestTorso
  components:
  - type: Organ
    category: Torso
  - type: Damageable
  - type: Injurable
  - type: Nerve
  - type: Woundable
    integrityCap: 200
    thresholds:
      Healthy: 200
      Minor: 160
      Moderate: 120
      Severe: 80
      Critical: 40
      Mangled: 14
      Severed: 0
";

    [Test]
    public async Task RecurringHealTicksDoNotBlockPassiveBleedHealing()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid attacker = default;
        EntityUid victim = default;
        EntityUid torso = default;

        await server.WaitPost(() =>
        {
            attacker = sEntMan.SpawnEntity("BleedHealTickTestAttacker", coords);
            victim = sEntMan.SpawnEntity("BleedHealTickTestVictim", coords);
            torso = sEntMan.SpawnEntity("BleedHealTickTestTorso", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(torso, organsContainer);

            sEntMan.GetComponent<TargetingComponent>(attacker).Target = TargetBodyPart.Chest;
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(BluntDamageType);
            sDamageable.TryChangeDamage(victim, new DamageSpecifier(proto, FixedPoint2.New(10)), ignoreResistances: false, origin: attacker);
        });

        var healProto = sProtoMan.Index(AsphyxiationDamageType);

        async Task HealTickSeconds(float seconds)
        {
            for (var i = 0; i < (int) seconds; i++)
            {
                await pair.RunSeconds(1f);
                await server.WaitPost(() =>
                {
                    sDamageable.TryChangeDamage(victim, new DamageSpecifier(healProto, FixedPoint2.New(-1)), ignoreResistances: true, interruptsDoAfters: false);
                });
            }
        }

        await HealTickSeconds(3f);

        FixedPoint2 bleedsBefore = default;
        await server.WaitAssertion(() =>
        {
            bleedsBefore = sEntMan.GetComponent<WoundableComponent>(torso).Bleeds;
            Assert.That(bleedsBefore, Is.GreaterThan(FixedPoint2.Zero),
                "A 10-blunt chest hit should leave the torso with a bleeding wound.");
        });

        await HealTickSeconds(7f);

        await server.WaitAssertion(() =>
        {
            var bleedsAfter = sEntMan.GetComponent<WoundableComponent>(torso).Bleeds;
            Assert.That(bleedsAfter, Is.LessThan(bleedsBefore),
                "Minor bleeding should keep clotting passively even while recurring heal ticks (respirator/bloodstream regen) hit the mob - heals must not reset the wound heal delay.");
        });
    }
}
