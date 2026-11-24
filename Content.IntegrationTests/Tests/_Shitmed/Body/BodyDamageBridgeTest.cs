// SPDX-FileCopyrightText: 2026 Maciej Walendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Shitmed.Body;
using Content.Shared._Shitmed.Medical.Surgery.Consciousness.Components;
using Content.Shared._Shitmed.Medical.Surgery.Pain.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

[TestFixture]
[TestOf(typeof(BodyDamageBridgeSystem))]
[TestOf(typeof(WoundSystem))]
public sealed class BodyDamageBridgeTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> BluntDamageType = "Blunt";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: BridgeTestAttacker
  components:
  - type: Targeting

- type: entity
  id: BridgeTestVictim
  components:
  - type: Body
  - type: Damageable
  - type: Injurable
  - type: Consciousness
    threshold: 95
    cap: 190

- type: entity
  id: BridgeTestBrainOrgan
  components:
  - type: Organ
    category: Head
  - type: ConsciousnessRequired
    identifier: nerveSystem
    causesDeath: true
  - type: NerveSystem

- type: entity
  id: BridgeTestTorsoOrgan
  components:
  - type: Organ
    category: Torso
  - type: Damageable
  - type: Injurable
  - type: Nerve
  - type: Woundable
    integrityCap: 30
    thresholds:
      Healthy: 30
      Minor: 24
      Moderate: 18
      Severe: 12
      Critical: 6
      Mangled: 2
      Severed: 0

- type: entity
  id: BridgeTestArmOrgan
  components:
  - type: Organ
    category: ArmLeft
  - type: Damageable
  - type: Injurable
  - type: Nerve
  - type: Woundable
    integrityCap: 30
    thresholds:
      Healthy: 30
      Minor: 24
      Moderate: 18
      Severe: 12
      Critical: 6
      Mangled: 2
      Severed: 0

";

    [Test]
    public async Task DamageMirrorsToTargetedLimbInducesWoundAndReplicates()
    {
        var pair = Pair;
        var server = pair.Server;
        var client = pair.Client;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var cEntMan = client.ResolveDependency<IEntityManager>();

        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid attacker = default;
        EntityUid victim = default;
        EntityUid organ = default;
        EntityUid brain = default;

        await server.WaitPost(() =>
        {
            attacker = sEntMan.SpawnEntity("BridgeTestAttacker", coords);
            victim = sEntMan.SpawnEntity("BridgeTestVictim", coords);
            brain = sEntMan.SpawnEntity("BridgeTestBrainOrgan", coords);
            organ = sEntMan.SpawnEntity("BridgeTestTorsoOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);

            // Brain must be inserted first: ConsciousnessSystem only resolves
            // ConsciousnessComponent.NerveSystem when the ConsciousnessRequired-tagged organ
            // is inserted, and PainSystem's per-organ Nerves rebuild (triggered by the torso's
            // own NerveComponent insertion) needs that NerveSystem already resolvable.
            container.Insert(brain, organsContainer);
            container.Insert(organ, organsContainer);
        });

        await pair.RunTicksSync(5);

        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(BluntDamageType);
            sDamageable.TryChangeDamage(victim, new DamageSpecifier(proto, FixedPoint2.New(10)), origin: attacker);
        });

        await pair.RunTicksSync(5);

        EntityUid wound = default;

        await server.WaitAssertion(() =>
        {
            var woundable = sEntMan.GetComponent<WoundableComponent>(organ);
            Assert.That(woundable.Wounds, Is.Not.Null);

            wound = woundable.Wounds!.ContainedEntities.Single();
            var woundComp = sEntMan.GetComponent<WoundComponent>(wound);

            Assert.Multiple(() =>
            {
                Assert.That(woundable.WoundableIntegrity, Is.EqualTo(FixedPoint2.New(20)));
                Assert.That(woundable.WoundableSeverity, Is.EqualTo(WoundableSeverity.Minor));
                Assert.That(sDamageable.GetTotalDamage(organ), Is.EqualTo(FixedPoint2.New(10)));

                Assert.That(woundComp.HoldingWoundable, Is.EqualTo(organ));
                Assert.That(woundComp.WoundSeverityPoint, Is.EqualTo(FixedPoint2.New(10)));
                Assert.That(woundComp.DamageType.Id, Is.EqualTo("Blunt"));

                // The mob's own pool is untouched by the limb mirror (mob-level
                // DamageableComponent stays authoritative for crit/death, independent of
                // per-limb wound tracking).
                Assert.That(sDamageable.GetTotalDamage(victim), Is.EqualTo(FixedPoint2.New(10)));

                // Pain: the wound's PainInflicterComponent should have picked up the wound's
                // severity as raw pain, and that should have propagated up into the brain
                // organ's NerveSystemComponent.Pain.
                var painInflicter = sEntMan.GetComponent<PainInflicterComponent>(wound);
                Assert.That(painInflicter.RawPain, Is.EqualTo(FixedPoint2.New(10)));

                var nerveSystem = sEntMan.GetComponent<NerveSystemComponent>(brain);
                Assert.That(nerveSystem.Pain, Is.EqualTo(FixedPoint2.New(8.7)));

                // Consciousness: pain should have registered as a negative consciousness
                // modifier on the mob, pulling its computed Consciousness value down from Cap
                // by exactly the nerve system's Pain (UpdateNerveSystemPain sets modifier to
                // -nerveSys.Pain).
                var consciousness = sEntMan.GetComponent<ConsciousnessComponent>(victim);
                Assert.That(consciousness.Modifiers, Does.ContainKey((brain, "WoundPain")));
                Assert.That(consciousness.Modifiers[(brain, "WoundPain")].Change, Is.EqualTo(FixedPoint2.New(-8.7)));
                Assert.That(consciousness.Consciousness, Is.EqualTo(consciousness.Cap - FixedPoint2.New(8.7)));
            });
        });

        var clientOrgan = cEntMan.GetEntity(sEntMan.GetNetEntity(organ));
        var clientWound = cEntMan.GetEntity(sEntMan.GetNetEntity(wound));
        var clientBrain = cEntMan.GetEntity(sEntMan.GetNetEntity(brain));
        var clientVictim = cEntMan.GetEntity(sEntMan.GetNetEntity(victim));
        var cDamageable = client.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();

        await client.WaitAssertion(() =>
        {
            var woundable = cEntMan.GetComponent<WoundableComponent>(clientOrgan);
            var woundComp = cEntMan.GetComponent<WoundComponent>(clientWound);

            Assert.Multiple(() =>
            {
                Assert.That(woundable.WoundableIntegrity, Is.EqualTo(FixedPoint2.New(20)));
                Assert.That(woundable.WoundableSeverity, Is.EqualTo(WoundableSeverity.Minor));
                Assert.That(cDamageable.GetTotalDamage(clientOrgan), Is.EqualTo(FixedPoint2.New(10)));

                Assert.That(woundComp.HoldingWoundable, Is.EqualTo(clientOrgan));
                Assert.That(woundComp.WoundSeverityPoint, Is.EqualTo(FixedPoint2.New(10)));
                Assert.That(woundComp.DamageType.Id, Is.EqualTo("Blunt"));

                var nerveSystem = cEntMan.GetComponent<NerveSystemComponent>(clientBrain);
                Assert.That(nerveSystem.Pain, Is.EqualTo(FixedPoint2.New(8.7)));

                var consciousness = cEntMan.GetComponent<ConsciousnessComponent>(clientVictim);
                Assert.That(consciousness.Consciousness, Is.EqualTo(consciousness.Cap - FixedPoint2.New(8.7)));
            });
        });
    }

    [Test]
    public async Task UntargetedDamageAppliesWeightedByPartType()
    {
        var pair = Pair;
        var server = pair.Server;
        var sEntMan = server.ResolveDependency<IEntityManager>();

        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid victim = default;
        EntityUid torso = default;
        EntityUid arm = default;

        await server.WaitPost(() =>
        {
            victim = sEntMan.SpawnEntity("BridgeTestVictim", coords);
            torso = sEntMan.SpawnEntity("BridgeTestTorsoOrgan", coords);
            arm = sEntMan.SpawnEntity("BridgeTestArmOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(torso, organsContainer);
            container.Insert(arm, organsContainer);
        });

        await pair.RunTicksSync(5);

        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(BluntDamageType);
            // No origin at all - mirrors environmental damage sources that never carry a
            // TargetingComponent-bearing attacker. The split is normalized to sum to the real 20
            // (weights 1.0 + 0.3 = 1.3): Arm (weight 0.3) gets 20*0.3/1.3 = 4.61, and the Torso
            // (heaviest, the anchor) soaks the exact remainder 20 - 4.61 = 15.39. Organs sum back
            // to the mob's own 20 - no phantom over-distribution to mask stranded damage.
            sDamageable.TryChangeDamage(victim, new DamageSpecifier(proto, FixedPoint2.New(20)));
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(sDamageable.GetTotalDamage(torso), Is.EqualTo(FixedPoint2.New(15.39)));
                Assert.That(sDamageable.GetTotalDamage(arm), Is.EqualTo(FixedPoint2.New(4.61)));
                Assert.That(sDamageable.GetTotalDamage(victim), Is.EqualTo(FixedPoint2.New(20)));
                Assert.That(sDamageable.GetTotalDamage(torso) + sDamageable.GetTotalDamage(arm),
                    Is.EqualTo(sDamageable.GetTotalDamage(victim)));
            });
        });
    }

    [Test]
    public async Task TinyUntargetedDamageDoesNotCreateAPrematurelyHealedWound()
    {
        var pair = Pair;
        var server = pair.Server;
        var sEntMan = server.ResolveDependency<IEntityManager>();

        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid victim = default;
        EntityUid torso = default;
        EntityUid arm = default;

        await server.WaitPost(() =>
        {
            victim = sEntMan.SpawnEntity("BridgeTestVictim", coords);
            torso = sEntMan.SpawnEntity("BridgeTestTorsoOrgan", coords);
            arm = sEntMan.SpawnEntity("BridgeTestArmOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(torso, organsContainer);
            container.Insert(arm, organsContainer);
        });

        await pair.RunTicksSync(5);

        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(BluntDamageType);
            // Both organs have integrityCap 30, so the Minor threshold scales to 0.3. Normalized
            // split of 0.1 over weights 1.0 + 0.3: Arm gets 0.1*0.3/1.3 = 0.02, Torso (anchor)
            // soaks the remainder 0.08 - both still below that line.
            sDamageable.TryChangeDamage(victim, new DamageSpecifier(proto, FixedPoint2.New(0.1)));
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(sDamageable.GetTotalDamage(torso), Is.EqualTo(FixedPoint2.New(0.08)));
                Assert.That(sDamageable.GetTotalDamage(arm), Is.EqualTo(FixedPoint2.New(0.02)));

                var torsoWoundable = sEntMan.GetComponent<WoundableComponent>(torso);
                Assert.That(torsoWoundable.Wounds, Is.Not.Null);
                Assert.That(torsoWoundable.Wounds!.ContainedEntities, Is.Empty,
                    "Damage below the Minor threshold shouldn't spawn a wound entity at all.");
            });
        });
    }

    [Test]
    public async Task SkipDamageBridgeComponentAlsoBlocksTheUntargetedFallback()
    {
        var pair = Pair;
        var server = pair.Server;
        var sEntMan = server.ResolveDependency<IEntityManager>();

        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid victim = default;
        EntityUid torso = default;
        EntityUid arm = default;

        await server.WaitPost(() =>
        {
            victim = sEntMan.SpawnEntity("BridgeTestVictim", coords);
            torso = sEntMan.SpawnEntity("BridgeTestTorsoOrgan", coords);
            arm = sEntMan.SpawnEntity("BridgeTestArmOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(torso, organsContainer);
            container.Insert(arm, organsContainer);
        });

        await pair.RunTicksSync(5);

        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(BluntDamageType);
            // No origin at all, marker present - mirrors exactly what BarotraumaSystem now
            // does around its own TryChangeDamage calls.
            sEntMan.AddComponent<SkipDamageBridgeComponent>(victim);
            sDamageable.TryChangeDamage(victim, new DamageSpecifier(proto, FixedPoint2.New(20)));
            sEntMan.RemoveComponent<SkipDamageBridgeComponent>(victim);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(sDamageable.GetTotalDamage(victim), Is.EqualTo(FixedPoint2.New(20)),
                    "The mob's own aggregate pool should still take the damage.");
                Assert.That(sDamageable.GetTotalDamage(torso), Is.EqualTo(FixedPoint2.Zero),
                    "Marked damage shouldn't reach any organ at all.");
                Assert.That(sDamageable.GetTotalDamage(arm), Is.EqualTo(FixedPoint2.Zero),
                    "Marked damage shouldn't reach any organ at all.");
            });
        });
    }

    [Test]
    public async Task OrganDamageChangesAutoSyncToTheMobUnlessMarkedBridgeOriginated()
    {
        var pair = Pair;
        var server = pair.Server;
        var sEntMan = server.ResolveDependency<IEntityManager>();

        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid victim = default;
        EntityUid torso = default;

        await server.WaitPost(() =>
        {
            victim = sEntMan.SpawnEntity("BridgeTestVictim", coords);
            torso = sEntMan.SpawnEntity("BridgeTestTorsoOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(torso, organsContainer);
        });

        await pair.RunTicksSync(5);

        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(BluntDamageType);
            // Direct organ-level write, no mob origin at all - simulates a surgery step or
            // WoundSystem inducing damage straight onto the organ, same place combat damage
            // eventually lands via the bridge's own mob->organ fan-out.
            sDamageable.TryChangeDamage(torso, new DamageSpecifier(proto, FixedPoint2.New(20)));
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(sDamageable.GetTotalDamage(torso), Is.EqualTo(FixedPoint2.New(20)));
                Assert.That(sDamageable.GetTotalDamage(victim), Is.EqualTo(FixedPoint2.New(20)),
                    "A direct organ damage write with no mob-level origin should still sync up to the mob's own pool.");
            });
        });

        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(BluntDamageType);
            // Heal the organ directly - no separate mob-level write anywhere in this test.
            sDamageable.TryChangeDamage(torso, new DamageSpecifier(proto, FixedPoint2.New(-8)), ignoreResistances: true);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(sDamageable.GetTotalDamage(torso), Is.EqualTo(FixedPoint2.New(12)));
                Assert.That(sDamageable.GetTotalDamage(victim), Is.EqualTo(FixedPoint2.New(12)),
                    "Healing the organ directly should auto-propagate to the mob with no explicit mob write.");
            });
        });

        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(BluntDamageType);
            // Same kind of heal, but marked as already having its own mob mirror elsewhere
            // (SurgerySystem.SetDamage's pattern) - the sync must skip this one entirely.
            sEntMan.AddComponent<SkipOrganMobSyncComponent>(torso);
            sDamageable.TryChangeDamage(torso, new DamageSpecifier(proto, FixedPoint2.New(-5)), ignoreResistances: true);
            sEntMan.RemoveComponent<SkipOrganMobSyncComponent>(torso);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(sDamageable.GetTotalDamage(torso), Is.EqualTo(FixedPoint2.New(7)));
                Assert.That(sDamageable.GetTotalDamage(victim), Is.EqualTo(FixedPoint2.New(12)),
                    "SkipOrganMobSyncComponent should suppress the auto-sync for this write.");
            });
        });
    }

    [Test]
    public async Task UntargetedHealingDistributesToOrgansTheSameWayUntargetedDamageDoes()
    {
        var pair = Pair;
        var server = pair.Server;
        var sEntMan = server.ResolveDependency<IEntityManager>();

        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid victim = default;
        EntityUid torso = default;
        EntityUid arm = default;

        await server.WaitPost(() =>
        {
            victim = sEntMan.SpawnEntity("BridgeTestVictim", coords);
            torso = sEntMan.SpawnEntity("BridgeTestTorsoOrgan", coords);
            arm = sEntMan.SpawnEntity("BridgeTestArmOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(torso, organsContainer);
            container.Insert(arm, organsContainer);
        });

        await pair.RunTicksSync(5);

        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(BluntDamageType);
            // Untargeted damage first, normalized onto both organs - Arm (weight 0.3) gets
            // 20*0.3/1.3 = 4.61, Torso (anchor) soaks the remainder 15.39, matching
            // UntargetedDamageAppliesWeightedByPartType.
            sDamageable.TryChangeDamage(victim, new DamageSpecifier(proto, FixedPoint2.New(20)));
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(sDamageable.GetTotalDamage(torso), Is.EqualTo(FixedPoint2.New(15.39)));
                Assert.That(sDamageable.GetTotalDamage(arm), Is.EqualTo(FixedPoint2.New(4.61)));
                Assert.That(sDamageable.GetTotalDamage(victim), Is.EqualTo(FixedPoint2.New(20)));
            });
        });

        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(BluntDamageType);
            // Untargeted HEAL, no origin - the exact call shape HealthChangeEntityEffectSystem
            // uses for a chem like Bicaridine (TryChangeDamage on the mob directly, no
            // TargetingComponent-bearing origin).
            sDamageable.TryChangeDamage(victim, new DamageSpecifier(proto, FixedPoint2.New(-10)), ignoreResistances: true);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(sDamageable.GetTotalDamage(torso), Is.EqualTo(FixedPoint2.New(7.69)),
                    "Torso (anchor) should have healed by the remainder 7.70 - it should NOT still show its pre-heal damage while the mob reads a lower total.");
                Assert.That(sDamageable.GetTotalDamage(arm), Is.EqualTo(FixedPoint2.New(2.31)),
                    "Arm (weight 0.3) should have healed by 2.30, same normalized weighting the deal side uses.");
                Assert.That(sDamageable.GetTotalDamage(victim), Is.EqualTo(FixedPoint2.New(10)),
                    "The mob's own pool should reflect the full nominal heal, same as before this fix - the bug was organs never seeing any of it, not the mob's own number being wrong.");
                Assert.That(sDamageable.GetTotalDamage(torso) + sDamageable.GetTotalDamage(arm),
                    Is.EqualTo(sDamageable.GetTotalDamage(victim)));
            });
        });
    }

    [Test]
    public async Task OriginDamageToAMissingLimbRedirectsToTheTorsoInsteadOfStranding()
    {
        var pair = Pair;
        var server = pair.Server;
        var sEntMan = server.ResolveDependency<IEntityManager>();

        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid attacker = default;
        EntityUid victim = default;
        EntityUid torso = default;

        await server.WaitPost(() =>
        {
            attacker = sEntMan.SpawnEntity("BridgeTestAttacker", coords);
            victim = sEntMan.SpawnEntity("BridgeTestVictim", coords);
            // Only a torso - the arm the attacker aims at is absent (dismembered).
            torso = sEntMan.SpawnEntity("BridgeTestTorsoOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(torso, organsContainer);

            // Aim at the left arm, which has no organ present.
            sEntMan.GetComponent<TargetingComponent>(attacker).Target = TargetBodyPart.LeftArm;
        });

        await pair.RunTicksSync(5);

        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(BluntDamageType);
            sDamageable.TryChangeDamage(victim, new DamageSpecifier(proto, FixedPoint2.New(10)), origin: attacker);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                // The full hit walked ArmLeft -> Torso and landed on the torso organ rather than
                // stranding on the mob's aggregate pool with no organ to heal it off.
                Assert.That(sDamageable.GetTotalDamage(torso), Is.EqualTo(FixedPoint2.New(10)));
                Assert.That(sDamageable.GetTotalDamage(victim), Is.EqualTo(FixedPoint2.New(10)));
                Assert.That(sDamageable.GetTotalDamage(torso), Is.EqualTo(sDamageable.GetTotalDamage(victim)),
                    "No organless remainder: every point on the mob is accounted for on an organ.");
            });
        });
    }
}
