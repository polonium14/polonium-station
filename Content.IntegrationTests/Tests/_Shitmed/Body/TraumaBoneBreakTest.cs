// SPDX-FileCopyrightText: 2026 Maciej Walendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Administration.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Medical;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

[TestFixture]
[TestOf(typeof(TraumaSystem))]
public sealed class TraumaBoneBreakTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> BluntDamageType = "Blunt";
    private static readonly ProtoId<DamageTypePrototype> PiercingDamageType = "Piercing";
    private static readonly ProtoId<DamageTypePrototype> PoisonDamageType = "Poison";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: BoneBreakTestAttacker
  components:
  - type: Targeting

- type: entity
  id: BoneBreakTestVictim
  components:
  - type: Body
  - type: Damageable
  - type: Injurable
  - type: Consciousness
    threshold: 95
    cap: 190
  - type: Bloodstream
    bloodlossDamage: {}
    bloodlossHealDamage: {}
    bloodReferenceSolution:
      reagents:
      - ReagentId: Blood
        Quantity: 300

- type: entity
  id: BoneBreakTestBrainOrgan
  components:
  - type: Organ
    category: Head
  - type: ConsciousnessRequired
    identifier: nerveSystem
    causesDeath: true
  - type: NerveSystem
  - type: OrganIntegrity
    integrityCap: 15
    integrityThresholds:
      Normal: 15
      Damaged: 6
      Destroyed: 0

- type: entity
  id: BoneBreakTestTorsoOrgan
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

- type: entity
  id: BoneBreakTestBrutepack
  components:
  - type: Healing
    damage:
      types:
        Blunt: -20

- type: entity
  id: BoneBreakTestBleedStopper
  components:
  - type: Healing
    damage:
      types: {}
    bloodlossModifier: -50
";

    /// <summary>
    /// The chance formula involves real RNG (~80% per qualifying hit per the static-analysis
    /// math for WoundBlunt), so a single trial isn't conclusive either way. Runs N independent
    /// fresh-organ trials and requires a healthy fraction to succeed - anywhere near 0/N would
    /// mean a real blocking bug, not bad luck.
    /// </summary>
    [Test]
    public async Task HardBluntHitCanBreakBone()
    {
        const int trials = 15;
        var successes = 0;

        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sTrauma = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<TraumaSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        for (var i = 0; i < trials; i++)
        {
            EntityUid attacker = default;
            EntityUid victim = default;
            EntityUid organ = default;
            EntityUid brain = default;

            await server.WaitPost(() =>
            {
                attacker = sEntMan.SpawnEntity("BoneBreakTestAttacker", coords);
                victim = sEntMan.SpawnEntity("BoneBreakTestVictim", coords);
                brain = sEntMan.SpawnEntity("BoneBreakTestBrainOrgan", coords);
                organ = sEntMan.SpawnEntity("BoneBreakTestTorsoOrgan", coords);

                var container = sEntMan.System<SharedContainerSystem>();
                var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
                // Brain must be inserted first - ApplyTraumas requires a resolvable NerveSystem
                // via ConsciousnessComponent, same ordering requirement as BodyDamageBridgeTest.
                container.Insert(brain, organsContainer);
                container.Insert(organ, organsContainer);
            });

            await pair.RunTicksSync(5);

            if (i == 0)
            {
                // Confirm the bone-spawn fix actually populated a bone before throwing damage
                // at it - if this fails, the real problem is upstream of the trauma roll.
                await server.WaitAssertion(() =>
                {
                    var woundable = sEntMan.GetComponent<WoundableComponent>(organ);
                    Assert.That(woundable.Bone, Is.Not.Null);
                    Assert.That(woundable.Bone!.ContainedEntities, Is.Not.Empty, "WoundableComponent.Bone container is empty - MapInit bone-spawn didn't fire.");
                });
            }

            // Well above WoundBlunt's severityThreshold=12 gate, resistances bypassed to
            // isolate the trauma-roll mechanic itself from armor mitigation (matches the
            // "robust toolbox, 20 Blunt" case the user actually tested with).
            await server.WaitPost(() =>
            {
                var proto = sProtoMan.Index(BluntDamageType);
                sDamageable.TryChangeDamage(organ, new DamageSpecifier(proto, FixedPoint2.New(20)), ignoreResistances: true, origin: attacker);
            });

            await pair.RunTicksSync(5);

            await server.WaitAssertion(() =>
            {
                if (sTrauma.TryGetWoundableTrauma(organ, out var traumas)
                    && traumas!.Any(t => t.Comp.TraumaType == TraumaType.BoneDamage))
                    successes++;
            });

            await server.WaitPost(() =>
            {
                sEntMan.DeleteEntity(victim);
                if (!sEntMan.Deleted(organ))
                    sEntMan.DeleteEntity(organ);
                sEntMan.DeleteEntity(attacker);
            });
        }

        const int minSuccesses = 6;
        Assert.That(successes, Is.GreaterThanOrEqualTo(minSuccesses),
            $"{successes}/{trials} independent 20-severity Blunt hits produced a BoneDamage trauma (needed at least {minSuccesses}) - static analysis predicts ~80% per hit, this few successes means the mechanic's rate has genuinely dropped, not unlucky.");
    }

    [Test]
    public async Task RepeatedHitsEscalateBoneSeverity()
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
        EntityUid organ = default;
        EntityUid brain = default;

        await server.WaitPost(() =>
        {
            attacker = sEntMan.SpawnEntity("BoneBreakTestAttacker", coords);
            victim = sEntMan.SpawnEntity("BoneBreakTestVictim", coords);
            brain = sEntMan.SpawnEntity("BoneBreakTestBrainOrgan", coords);
            organ = sEntMan.SpawnEntity("BoneBreakTestTorsoOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(brain, organsContainer);
            container.Insert(organ, organsContainer);
        });

        await pair.RunTicksSync(5);

        var woundable = sEntMan.GetComponent<WoundableComponent>(organ);
        var boneEnt = woundable.Bone!.ContainedEntities.First();
        var initialBoneIntegrity = sEntMan.GetComponent<BoneComponent>(boneEnt).BoneIntegrity;

        for (var i = 0; i < 10; i++)
        {
            await server.WaitPost(() =>
            {
                var proto = sProtoMan.Index(BluntDamageType);
                sDamageable.TryChangeDamage(organ, new DamageSpecifier(proto, FixedPoint2.New(20)), ignoreResistances: true, origin: attacker);
            });

            await pair.RunTicksSync(5);

            await server.WaitAssertion(() =>
            {
                var bone = sEntMan.GetComponent<BoneComponent>(boneEnt);
                var woundableComp = sEntMan.GetComponent<WoundableComponent>(organ);
                TestContext.Out.WriteLine($"Hit {i + 1}: BoneIntegrity={bone.BoneIntegrity} BoneSeverity={bone.BoneSeverity} WoundableIntegrity={woundableComp.WoundableIntegrity} WoundableSeverity={woundableComp.WoundableSeverity}");
            });
        }

        await server.WaitAssertion(() =>
        {
            var finalBoneIntegrity = sEntMan.GetComponent<BoneComponent>(boneEnt).BoneIntegrity;
            Assert.That(finalBoneIntegrity, Is.LessThan(initialBoneIntegrity),
                "10 qualifying hits (~80% break chance each) should have worsened the bone's integrity at least once.");
        });
    }

    /// <summary>
    /// Same as RepeatedHitsEscalateBoneSeverity but hits the MOB (not the organ directly),
    /// mirroring the real melee/BodyDamageBridgeSystem path (attacker's TargetingComponent
    /// selects the organ, resistances NOT bypassed) to check whether armor/damage-modifier
    /// attenuation between mob and organ is what's actually stalling real-gameplay progression.
    /// </summary>
    [Test]
    public async Task RepeatedRealisticHitsEscalateBoneSeverity()
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
        EntityUid organ = default;
        EntityUid brain = default;

        await server.WaitPost(() =>
        {
            attacker = sEntMan.SpawnEntity("BoneBreakTestAttacker", coords);
            victim = sEntMan.SpawnEntity("BoneBreakTestVictim", coords);
            brain = sEntMan.SpawnEntity("BoneBreakTestBrainOrgan", coords);
            organ = sEntMan.SpawnEntity("BoneBreakTestTorsoOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(brain, organsContainer);
            container.Insert(organ, organsContainer);
        });

        await pair.RunTicksSync(5);

        var woundable = sEntMan.GetComponent<WoundableComponent>(organ);
        var boneEnt = woundable.Bone!.ContainedEntities.First();
        var initialBoneIntegrity = sEntMan.GetComponent<BoneComponent>(boneEnt).BoneIntegrity;

        for (var i = 0; i < 10; i++)
        {
            await server.WaitPost(() =>
            {
                var proto = sProtoMan.Index(BluntDamageType);
                // Hits the MOB, resistances NOT bypassed - matches a real robust toolbox swing.
                sDamageable.TryChangeDamage(victim, new DamageSpecifier(proto, FixedPoint2.New(20)), ignoreResistances: false, origin: attacker);
            });

            await pair.RunTicksSync(5);

            await server.WaitAssertion(() =>
            {
#pragma warning disable CS0618
                var mobDamageTotal = sDamageable.GetTotalDamage(victim);
#pragma warning restore CS0618
                var bone = sEntMan.GetComponent<BoneComponent>(boneEnt);
                var woundableComp = sEntMan.GetComponent<WoundableComponent>(organ);
                TestContext.Out.WriteLine($"Hit {i + 1}: MobDamageTotal={mobDamageTotal} BoneIntegrity={bone.BoneIntegrity} BoneSeverity={bone.BoneSeverity} WoundableIntegrity={woundableComp.WoundableIntegrity} WoundableSeverity={woundableComp.WoundableSeverity}");
            });
        }

        await server.WaitAssertion(() =>
        {
            var finalBoneIntegrity = sEntMan.GetComponent<BoneComponent>(boneEnt).BoneIntegrity;
            Assert.That(finalBoneIntegrity, Is.LessThan(initialBoneIntegrity),
                "10 qualifying hits (~80% break chance each) should have worsened the bone's integrity at least once.");
        });
    }

    [Test]
    public async Task WoundBleedingPopulatesWoundableBleeds()
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
        EntityUid organ = default;
        EntityUid brain = default;

        await server.WaitPost(() =>
        {
            attacker = sEntMan.SpawnEntity("BoneBreakTestAttacker", coords);
            victim = sEntMan.SpawnEntity("BoneBreakTestVictim", coords);
            brain = sEntMan.SpawnEntity("BoneBreakTestBrainOrgan", coords);
            organ = sEntMan.SpawnEntity("BoneBreakTestTorsoOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(brain, organsContainer);
            container.Insert(organ, organsContainer);
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(PiercingDamageType);
            // WoundBlunt's BleedInflicter has severityThreshold: 8 - a 20-severity hit clears it.
            sDamageable.TryChangeDamage(organ, new DamageSpecifier(proto, FixedPoint2.New(20)), ignoreResistances: true, origin: attacker);
        });

        // Several bloodstream ticks so UpdateWounds' IsBleeding flip + RecomputeWoundableBleeds
        // both get a chance to run.
        await pair.RunTicksSync(20);

        await server.WaitAssertion(() =>
        {
            var woundableComp = sEntMan.GetComponent<WoundableComponent>(organ);
            Assert.That(woundableComp.Bleeds, Is.GreaterThan(FixedPoint2.Zero),
                "WoundableComponent.Bleeds stayed zero after a wound crossed BleedInflicter's severityThreshold - the per-tick aggregation isn't reaching this woundable.");
        });
    }

    [Test]
    public async Task BrokenBoneBlocksWoundHealing()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sWound = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<WoundSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid attacker = default;
        EntityUid victim = default;
        EntityUid organ = default;
        EntityUid brain = default;

        await server.WaitPost(() =>
        {
            attacker = sEntMan.SpawnEntity("BoneBreakTestAttacker", coords);
            victim = sEntMan.SpawnEntity("BoneBreakTestVictim", coords);
            brain = sEntMan.SpawnEntity("BoneBreakTestBrainOrgan", coords);
            organ = sEntMan.SpawnEntity("BoneBreakTestTorsoOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(brain, organsContainer);
            container.Insert(organ, organsContainer);
        });

        // A real hit's chance to induce a BoneDamage trauma is genuinely RNG-based (see
        // HardBluntHitCanBreakBone above) - this test isn't about that roll, so deal the damage
        // for wounds/integrity but drive the bone straight to Broken deterministically via
        // ApplyBoneTrauma (not the lower-level ApplyDamageToBone - the healing-block check reads
        // an actual TraumaComponent off the wound's TraumaContainer, which only ApplyBoneTrauma's
        // AddTrauma call creates; skipping straight to ApplyDamageToBone would break the bone
        // without the bookkeeping the blocker depends on).
        var sTrauma = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<TraumaSystem>();

        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(BluntDamageType);
            sDamageable.TryChangeDamage(organ, new DamageSpecifier(proto, FixedPoint2.New(20)), ignoreResistances: true, origin: attacker);
        });

        await pair.RunTicksSync(5);

        FixedPoint2 severityBeforeHeal = default;

        await server.WaitAssertion(() =>
        {
            var woundableComp = sEntMan.GetComponent<WoundableComponent>(organ);
            var boneEnt = woundableComp.Bone!.ContainedEntities.First();
            var bone = sEntMan.GetComponent<BoneComponent>(boneEnt);
            var wound = sWound.GetWoundableWounds(organ, woundableComp).First();
            var inflicterComp = sEntMan.GetComponent<TraumaInflicterComponent>(wound);

            sTrauma.ApplyBoneTrauma(boneEnt, (organ, woundableComp), (wound, inflicterComp), bone.BoneIntegrity, bone);
            Assert.That(bone.BoneSeverity, Is.EqualTo(BoneSeverity.Broken), "Setup didn't actually reach Broken - can't test the healing block.");

            severityBeforeHeal = woundableComp.WoundableIntegrity;
        });

        await server.WaitPost(() =>
        {
            sWound.TryHealWoundsOnWoundable(organ, FixedPoint2.New(100), out var healed);
        });

        await server.WaitAssertion(() =>
        {
            var woundable = sEntMan.GetComponent<WoundableComponent>(organ);
            Assert.That(woundable.WoundableIntegrity, Is.EqualTo(severityBeforeHeal),
                "Woundable healed despite a Broken bone - WoundHealAttemptEvent's blocker isn't firing.");
        });
    }

    [Test]
    public async Task RejuvenateHealsBrokenBonesAndWounds()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sTrauma = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<TraumaSystem>();
        var sWound = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<WoundSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid attacker = default;
        EntityUid victim = default;
        EntityUid organ = default;
        EntityUid brain = default;

        await server.WaitPost(() =>
        {
            attacker = sEntMan.SpawnEntity("BoneBreakTestAttacker", coords);
            victim = sEntMan.SpawnEntity("BoneBreakTestVictim", coords);
            brain = sEntMan.SpawnEntity("BoneBreakTestBrainOrgan", coords);
            organ = sEntMan.SpawnEntity("BoneBreakTestTorsoOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(brain, organsContainer);
            container.Insert(organ, organsContainer);
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(BluntDamageType);
            sDamageable.TryChangeDamage(organ, new DamageSpecifier(proto, FixedPoint2.New(20)), ignoreResistances: true, origin: attacker);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var woundableComp = sEntMan.GetComponent<WoundableComponent>(organ);
            var boneEnt = woundableComp.Bone!.ContainedEntities.First();
            var bone = sEntMan.GetComponent<BoneComponent>(boneEnt);
            var wound = sWound.GetWoundableWounds(organ, woundableComp).First();
            var inflicterComp = sEntMan.GetComponent<TraumaInflicterComponent>(wound);

            sTrauma.ApplyBoneTrauma(boneEnt, (organ, woundableComp), (wound, inflicterComp), bone.BoneIntegrity, bone);
            Assert.That(bone.BoneSeverity, Is.EqualTo(BoneSeverity.Broken), "Setup didn't actually reach Broken.");
        });

        // Poison specifically, since it's the damage type this doesn't go through
        // WoundSystem's wound-induction path the same way Blunt/etc does (no WoundBlunt-style
        // prototype tied to it in this fixture) - isolates the organ's raw
        // DamageableComponent.Damage total from the wound-healing path already covered above.
        await server.WaitPost(() =>
        {
            var poisonProto = sProtoMan.Index(PoisonDamageType);
            sDamageable.TryChangeDamage(organ, new DamageSpecifier(poisonProto, FixedPoint2.New(10)), ignoreResistances: true, origin: attacker);
        });

        await pair.RunTicksSync(5);

        var sRejuvenate = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<RejuvenateSystem>();

        await server.WaitPost(() =>
        {
            sRejuvenate.PerformRejuvenate(victim);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var woundableComp = sEntMan.GetComponent<WoundableComponent>(organ);
            var boneEnt = woundableComp.Bone!.ContainedEntities.First();
            var bone = sEntMan.GetComponent<BoneComponent>(boneEnt);

            Assert.That(bone.BoneSeverity, Is.EqualTo(BoneSeverity.Normal), "Rejuvenate didn't heal the broken bone.");
            Assert.That(woundableComp.WoundableIntegrity, Is.EqualTo(woundableComp.IntegrityCap), "Rejuvenate didn't heal the wound.");
            Assert.That(sTrauma.HasWoundableTrauma(organ), Is.False, "Rejuvenate left a stale BoneDamage trauma behind.");

#pragma warning disable CS0618
            var organTotalDamage = sDamageable.GetTotalDamage(organ);
#pragma warning restore CS0618
            Assert.That(organTotalDamage, Is.EqualTo(FixedPoint2.Zero),
                "Rejuvenate cleared the mob's health but left raw damage (e.g. Poison/toxin) on the organ's own DamageableComponent.");
        });
    }

    [Test]
    public async Task FreshOrganStartsAtFullIntegrity()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid brain = default;

        await server.WaitPost(() =>
        {
            brain = sEntMan.SpawnEntity("BoneBreakTestBrainOrgan", coords);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var integrity = sEntMan.GetComponent<OrganIntegrityComponent>(brain);
            Assert.That(integrity.OrganIntegrity, Is.EqualTo(integrity.IntegrityCap),
                "Fresh organ's OrganIntegrity didn't seed to IntegrityCap on init.");
        });
    }

    [Test]
    public async Task WoundBleedingDrivesMobBleedAmountAndHealingStopsIt()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sWound = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<WoundSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid attacker = default;
        EntityUid victim = default;
        EntityUid organ = default;
        EntityUid brain = default;

        await server.WaitPost(() =>
        {
            attacker = sEntMan.SpawnEntity("BoneBreakTestAttacker", coords);
            victim = sEntMan.SpawnEntity("BoneBreakTestVictim", coords);
            brain = sEntMan.SpawnEntity("BoneBreakTestBrainOrgan", coords);
            organ = sEntMan.SpawnEntity("BoneBreakTestTorsoOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(brain, organsContainer);
            container.Insert(organ, organsContainer);
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(PiercingDamageType);
            // WoundBlunt's BleedInflicter has severityThreshold: 8 - a 20-severity hit clears it.
            sDamageable.TryChangeDamage(organ, new DamageSpecifier(proto, FixedPoint2.New(20)), ignoreResistances: true, origin: attacker);
        });

        // Several bloodstream ticks so UpdateWounds' IsBleeding flip + both recomputes
        // (WoundableComponent.Bleeds and BloodstreamComponent.BleedAmountFromWounds) run.
        await pair.RunTicksSync(20);

        await server.WaitAssertion(() =>
        {
            var bloodstream = sEntMan.GetComponent<BloodstreamComponent>(victim);
            Assert.That(bloodstream.BleedAmount, Is.GreaterThan(0),
                "Mob's BloodstreamComponent.BleedAmount stayed zero despite an active wound bleed - the wound-to-mob wiring isn't reaching the mob.");
        });

        await server.WaitPost(() =>
        {
            var woundableComp = sEntMan.GetComponent<WoundableComponent>(organ);
            sWound.TryHaltAllBleeding(organ, woundableComp);
        });

        // A few more ticks so the per-tick recompute propagates the now-cleared per-wound state
        // back up to both WoundableComponent.Bleeds and the mob's BleedAmount.
        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            var woundableComp = sEntMan.GetComponent<WoundableComponent>(organ);
            Assert.That(woundableComp.Bleeds, Is.EqualTo(FixedPoint2.Zero),
                "TryHaltAllBleeding didn't actually clear the wound's own bleed state - WoundableComponent.Bleeds got recomputed right back to nonzero.");

            var bloodstream = sEntMan.GetComponent<BloodstreamComponent>(victim);
            Assert.That(bloodstream.BleedAmount, Is.EqualTo(0f),
                "Mob's BloodstreamComponent.BleedAmount didn't follow the halted wound bleed back down to zero.");
        });
    }

    [Test]
    public async Task BrokenBoneBlocksTopicalHealing()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sTrauma = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<TraumaSystem>();
        var sWound = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<WoundSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid attacker = default;
        EntityUid victim = default;
        EntityUid organ = default;
        EntityUid brain = default;

        await server.WaitPost(() =>
        {
            attacker = sEntMan.SpawnEntity("BoneBreakTestAttacker", coords);
            victim = sEntMan.SpawnEntity("BoneBreakTestVictim", coords);
            brain = sEntMan.SpawnEntity("BoneBreakTestBrainOrgan", coords);
            organ = sEntMan.SpawnEntity("BoneBreakTestTorsoOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(brain, organsContainer);
            container.Insert(organ, organsContainer);
        });

        await pair.RunTicksSync(5);

        // Deal damage to the MOB (not the organ directly), with the attacker's default
        // TargetingComponent selection (Chest -> Torso), matching how BodyDamageBridgeSystem
        // mirrors combat damage onto the organ - same pattern as
        // RepeatedRealisticHitsEscalateBoneSeverity above.
        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(BluntDamageType);
            sDamageable.TryChangeDamage(victim, new DamageSpecifier(proto, FixedPoint2.New(20)), ignoreResistances: false, origin: attacker);
        });

        await pair.RunTicksSync(5);

        // Drive the bone straight to Broken deterministically - same setup as
        // BrokenBoneBlocksWoundHealing above.
        await server.WaitAssertion(() =>
        {
            var woundableComp = sEntMan.GetComponent<WoundableComponent>(organ);
            var boneEnt = woundableComp.Bone!.ContainedEntities.First();
            var bone = sEntMan.GetComponent<BoneComponent>(boneEnt);
            var wound = sWound.GetWoundableWounds(organ, woundableComp).First();
            var inflicterComp = sEntMan.GetComponent<TraumaInflicterComponent>(wound);

            sTrauma.ApplyBoneTrauma(boneEnt, (organ, woundableComp), (wound, inflicterComp), bone.BoneIntegrity, bone);
            Assert.That(bone.BoneSeverity, Is.EqualTo(BoneSeverity.Broken), "Setup didn't actually reach Broken - can't test the healing block.");
        });

        FixedPoint2 mobDamageBeforeHeal = default;
        await server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            mobDamageBeforeHeal = sDamageable.GetTotalDamage(victim);
#pragma warning restore CS0618
            Assert.That(mobDamageBeforeHeal, Is.GreaterThan(FixedPoint2.Zero), "Setup didn't actually leave brute damage on the mob to try healing.");
        });

        await server.WaitPost(() =>
        {
            var brutepack = sEntMan.SpawnEntity("BoneBreakTestBrutepack", coords);
            var doAfterArgs = new DoAfterArgs(sEntMan, attacker, TimeSpan.FromSeconds(1), new HealingDoAfterEvent(), victim, target: victim, used: brutepack);
            var ev = new HealingDoAfterEvent
            {
                DoAfter = new Content.Shared.DoAfter.DoAfter(0, doAfterArgs, TimeSpan.Zero),
            };
            sEntMan.EventBus.RaiseLocalEvent(victim, ev);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
#pragma warning disable CS0618
            var mobDamageAfterHeal = sDamageable.GetTotalDamage(victim);
#pragma warning restore CS0618
            Assert.That(mobDamageAfterHeal, Is.EqualTo(mobDamageBeforeHeal),
                "Topical healing reduced brute damage on the mob despite a Broken bone on the targeted limb - HealingSystem isn't respecting TraumaSystem.TraumasBlockingHealing.");
        });
    }

    [Test]
    public async Task BandageStopsRealWoundBleeding()
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
        EntityUid organ = default;
        EntityUid brain = default;

        await server.WaitPost(() =>
        {
            attacker = sEntMan.SpawnEntity("BoneBreakTestAttacker", coords);
            victim = sEntMan.SpawnEntity("BoneBreakTestVictim", coords);
            brain = sEntMan.SpawnEntity("BoneBreakTestBrainOrgan", coords);
            organ = sEntMan.SpawnEntity("BoneBreakTestTorsoOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(brain, organsContainer);
            container.Insert(organ, organsContainer);
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(PiercingDamageType);
            // WoundBlunt's BleedInflicter has severityThreshold: 8 - a 20-severity hit clears it.
            // Dealt to the mob (not the organ directly) so BodyDamageBridgeSystem mirrors it,
            // same as the mob's own bleed-wiring test above.
            sDamageable.TryChangeDamage(victim, new DamageSpecifier(proto, FixedPoint2.New(20)), ignoreResistances: false, origin: attacker);
        });

        // Several bloodstream ticks so IsBleeding flip + both recomputes (WoundableComponent.Bleeds
        // and BloodstreamComponent.BleedAmountFromWounds) run before we try to heal it.
        await pair.RunTicksSync(20);

        await server.WaitAssertion(() =>
        {
            var woundableComp = sEntMan.GetComponent<WoundableComponent>(organ);
            var bloodstream = sEntMan.GetComponent<BloodstreamComponent>(victim);
            Assert.That(woundableComp.Bleeds, Is.GreaterThan(FixedPoint2.Zero), "Setup didn't actually leave the organ bleeding.");
            Assert.That(bloodstream.BleedAmount, Is.GreaterThan(0), "Setup didn't actually leave the mob bleeding.");
        });

        await server.WaitPost(() =>
        {
            var bandage = sEntMan.SpawnEntity("BoneBreakTestBleedStopper", coords);
            var doAfterArgs = new DoAfterArgs(sEntMan, attacker, TimeSpan.FromSeconds(1), new HealingDoAfterEvent(), victim, target: victim, used: bandage);
            var ev = new HealingDoAfterEvent
            {
                DoAfter = new Content.Shared.DoAfter.DoAfter(0, doAfterArgs, TimeSpan.Zero),
            };
            sEntMan.EventBus.RaiseLocalEvent(victim, ev);
        });

        // A few more ticks so the per-tick recompute propagates the now-cleared per-wound bleed
        // state back up to both WoundableComponent.Bleeds and the mob's BleedAmount.
        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            var woundableComp = sEntMan.GetComponent<WoundableComponent>(organ);
            Assert.That(woundableComp.Bleeds, Is.EqualTo(FixedPoint2.Zero),
                "Bandaging didn't actually clear the organ's real per-wound bleed state.");

            var bloodstream = sEntMan.GetComponent<BloodstreamComponent>(victim);
            Assert.That(bloodstream.BleedAmount, Is.EqualTo(0f),
                "Bandaging cleared the wound's bleed state but the mob's own BleedAmount/alert didn't follow it back down to zero.");
        });
    }

    [Test]
    public async Task BandageStopsBleedingEvenWhenBoneBrokenOnSameOrgan()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sTrauma = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<TraumaSystem>();
        var sWound = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<WoundSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid attacker = default;
        EntityUid victim = default;
        EntityUid organ = default;
        EntityUid brain = default;

        await server.WaitPost(() =>
        {
            attacker = sEntMan.SpawnEntity("BoneBreakTestAttacker", coords);
            victim = sEntMan.SpawnEntity("BoneBreakTestVictim", coords);
            brain = sEntMan.SpawnEntity("BoneBreakTestBrainOrgan", coords);
            organ = sEntMan.SpawnEntity("BoneBreakTestTorsoOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(brain, organsContainer);
            container.Insert(organ, organsContainer);
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(PiercingDamageType);
            sDamageable.TryChangeDamage(victim, new DamageSpecifier(proto, FixedPoint2.New(20)), ignoreResistances: false, origin: attacker);
        });

        await pair.RunTicksSync(20);

        // Drive the same organ's bone to Broken on top of the bleeding wound already induced.
        await server.WaitAssertion(() =>
        {
            var woundableComp = sEntMan.GetComponent<WoundableComponent>(organ);
            var boneEnt = woundableComp.Bone!.ContainedEntities.First();
            var bone = sEntMan.GetComponent<BoneComponent>(boneEnt);
            var wound = sWound.GetWoundableWounds(organ, woundableComp).First();
            var inflicterComp = sEntMan.GetComponent<TraumaInflicterComponent>(wound);

            sTrauma.ApplyBoneTrauma(boneEnt, (organ, woundableComp), (wound, inflicterComp), bone.BoneIntegrity, bone);
            Assert.That(bone.BoneSeverity, Is.EqualTo(BoneSeverity.Broken), "Setup didn't actually reach Broken - can't test the combined case.");
            Assert.That(woundableComp.Bleeds, Is.GreaterThan(FixedPoint2.Zero), "Setup didn't actually leave the organ bleeding.");
        });

        await server.WaitPost(() =>
        {
            var bandage = sEntMan.SpawnEntity("BoneBreakTestBleedStopper", coords);
            var doAfterArgs = new DoAfterArgs(sEntMan, attacker, TimeSpan.FromSeconds(1), new HealingDoAfterEvent(), victim, target: victim, used: bandage);
            var ev = new HealingDoAfterEvent
            {
                DoAfter = new Content.Shared.DoAfter.DoAfter(0, doAfterArgs, TimeSpan.Zero),
            };
            sEntMan.EventBus.RaiseLocalEvent(victim, ev);
        });

        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            var woundableComp = sEntMan.GetComponent<WoundableComponent>(organ);
            Assert.That(woundableComp.Bleeds, Is.EqualTo(FixedPoint2.Zero),
                "Bandaging should still stop bleeding on a limb with a Broken bone - only the brute/burn damage heal should be blocked, not the bloodloss-stop branch.");

            var boneEnt = woundableComp.Bone!.ContainedEntities.First();
            var bone = sEntMan.GetComponent<BoneComponent>(boneEnt);
            Assert.That(bone.BoneSeverity, Is.EqualTo(BoneSeverity.Broken),
                "The bone itself should still be Broken - a bandage isn't supposed to fix that, only surgery should.");
        });
    }

    [Test]
    public async Task HealingDoesNotCorruptWoundEnumeration()
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
        EntityUid organ = default;
        EntityUid brain = default;

        await server.WaitPost(() =>
        {
            attacker = sEntMan.SpawnEntity("BoneBreakTestAttacker", coords);
            victim = sEntMan.SpawnEntity("BoneBreakTestVictim", coords);
            brain = sEntMan.SpawnEntity("BoneBreakTestBrainOrgan", coords);
            organ = sEntMan.SpawnEntity("BoneBreakTestTorsoOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(brain, organsContainer);
            container.Insert(organ, organsContainer);
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(BluntDamageType);
            // Below WoundBlunt's severityThreshold=12, so this stays a plain small wound - no
            // trauma roll involved, keeps this test isolated to the healing-iteration bug.
            sDamageable.TryChangeDamage(organ, new DamageSpecifier(proto, FixedPoint2.New(5)), ignoreResistances: true, origin: attacker);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var proto = sProtoMan.Index(BluntDamageType);
            Assert.DoesNotThrow(() =>
                sDamageable.TryChangeDamage(organ, new DamageSpecifier(proto, FixedPoint2.New(-5)), ignoreResistances: true, origin: attacker),
                "Healing a wound's severity to zero crashed instead of cleanly removing it.");
        });
    }

    /// <summary>
    /// Coverage for the "TODO: Fill this with other blocking traumas" gap in
    /// TraumaSystem.Process.cs's HasAssociatedTrauma - BoneDamage already correctly only
    /// counted as blocking (showAll: false) when the bone was actually Broken, but OrganDamage
    /// had no equivalent severity gate, so a single point of organ damage (OrganSeverity.Damaged)
    /// counted the same as a fully OrganSeverity.Destroyed organ. Sets the brain's severity
    /// directly (not through the real damage pipeline, which is probabilistic - this test is
    /// only about the severity gate in HasAssociatedTrauma, not about how organ damage accrues).
    /// </summary>
    [Test]
    public async Task OnlyDestroyedOrganDamageBlocksHealing()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sTrauma = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<TraumaSystem>();
        var sWound = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<WoundSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid attacker = default;
        EntityUid victim = default;
        EntityUid organ = default;
        EntityUid brain = default;

        await server.WaitPost(() =>
        {
            attacker = sEntMan.SpawnEntity("BoneBreakTestAttacker", coords);
            victim = sEntMan.SpawnEntity("BoneBreakTestVictim", coords);
            brain = sEntMan.SpawnEntity("BoneBreakTestBrainOrgan", coords);
            organ = sEntMan.SpawnEntity("BoneBreakTestTorsoOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(brain, organsContainer);
            container.Insert(organ, organsContainer);
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(BluntDamageType);
            // Above WoundBlunt's severityThreshold=12, just to get a real wound with a real
            // TraumaInflicterComponent to attach the OrganDamage trauma to.
            sDamageable.TryChangeDamage(organ, new DamageSpecifier(proto, FixedPoint2.New(20)), ignoreResistances: true, origin: attacker);
        });

        await pair.RunTicksSync(5);

        EntityUid organWoundable = organ;
        await server.WaitAssertion(() =>
        {
            var woundableComp = sEntMan.GetComponent<WoundableComponent>(organ);
            var wound = sWound.GetWoundableWounds(organ, woundableComp).First();
            var inflicterComp = sEntMan.GetComponent<TraumaInflicterComponent>(wound);

            // Mirrors ApplyTraumas' real OrganDamage shape: the trauma's inflicter/holding
            // woundable is the wound on the hit limb (Torso), but TraumaTarget is the actual
            // vital organ being damaged (the brain), matching how a real hit picks a random
            // organ with OrganIntegrityComponent from the body.
            sTrauma.AddTrauma(brain, (organ, woundableComp), (wound, inflicterComp), TraumaType.OrganDamage, FixedPoint2.New(5));

            var brainIntegrity = sEntMan.GetComponent<OrganIntegrityComponent>(brain);
            brainIntegrity.OrganSeverity = OrganSeverity.Damaged;

            Assert.That(sTrauma.HasWoundableTrauma(organWoundable, TraumaType.OrganDamage, woundableComp, showAll: false), Is.False,
                "A merely Damaged organ (not Destroyed) shouldn't count as a blocking trauma - it should still be topically healable.");

            brainIntegrity.OrganSeverity = OrganSeverity.Destroyed;

            Assert.That(sTrauma.HasWoundableTrauma(organWoundable, TraumaType.OrganDamage, woundableComp, showAll: false), Is.True,
                "A Destroyed organ should count as a blocking trauma, same as a Broken bone does.");
        });
    }
}
