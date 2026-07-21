using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Shitmed.Medical.Surgery.Consciousness.Components;
using Content.Shared._Shitmed.Medical.Surgery.Pain.Components;
using Content.Shared.Administration.Systems;
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
[TestOf(typeof(RejuvenateSystem))]
public sealed class ConsciousnessPainDictMutationTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> BluntDamageType = "Blunt";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: DictMutationTestVictim
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
  id: DictMutationTestBrainOrgan
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
  id: DictMutationTestTorsoOrgan
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
    public async Task RejuvenateDoesNotThrowOnAWoundedEntity()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sDamageable = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<DamageableSystem>();
        var sRejuvenate = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<RejuvenateSystem>();
        var sProtoMan = server.ResolveDependency<IPrototypeManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid victim = default;
        EntityUid brain = default;
        EntityUid torso = default;

        await server.WaitPost(() =>
        {
            victim = sEntMan.SpawnEntity("DictMutationTestVictim", coords);
            brain = sEntMan.SpawnEntity("DictMutationTestBrainOrgan", coords);
            torso = sEntMan.SpawnEntity("DictMutationTestTorsoOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            // Brain first - resolving NerveSystem requires it, same ordering as TraumaBoneBreakTest.
            container.Insert(brain, organsContainer);
            container.Insert(torso, organsContainer);
        });

        await pair.RunTicksSync(5);

        // Deal real wound-inducing damage - this is what actually leaves a pain modifier behind
        // on the nerve system (and, via PainSystem's own sync, a Pain-typed consciousness
        // modifier) for Rejuvenate to trip over.
        await server.WaitPost(() =>
        {
            var proto = sProtoMan.Index(BluntDamageType);
            sDamageable.TryChangeDamage(torso, new DamageSpecifier(proto, FixedPoint2.New(20)), ignoreResistances: true);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var brainComp = sEntMan.GetComponent<NerveSystemComponent>(brain);
            Assert.That(brainComp.Modifiers, Is.Not.Empty,
                "Sanity check: the hit should have left at least one pain modifier behind for Rejuvenate to trip over.");
        });

        await server.WaitPost(() => sRejuvenate.PerformRejuvenate(victim));

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var brainComp = sEntMan.GetComponent<NerveSystemComponent>(brain);
            Assert.That(brainComp.Pain, Is.EqualTo(FixedPoint2.Zero), "Rejuvenate should have brought pain back down to zero.");
        });
    }
}
