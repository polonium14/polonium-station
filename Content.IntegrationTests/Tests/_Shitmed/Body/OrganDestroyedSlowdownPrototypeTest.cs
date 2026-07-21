using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared.Body;
using Content.Shared.FixedPoint;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

[TestFixture]
[TestOf(typeof(TraumaSystem))]
public sealed class OrganDestroyedSlowdownPrototypeTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: SlowdownProtoTestVictim
  components:
  - type: Body
  - type: Consciousness
    threshold: 95
    cap: 190

- type: entity
  id: SlowdownProtoTestBrainOrgan
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
  id: SlowdownProtoTestTorsoOrgan
  components:
  - type: Organ
    category: Torso
  - type: OrganIntegrity
    integrityCap: 15
    integrityThresholds:
      Normal: 15
      Damaged: 6
      Destroyed: 0
";

    [Test]
    public async Task DestroyingAnOrganOnALivingBodyDoesNotThrowOnTheSlowdownEffect()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sTrauma = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<TraumaSystem>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid victim = default;
        EntityUid brain = default;
        EntityUid torso = default;

        await server.WaitPost(() =>
        {
            victim = sEntMan.SpawnEntity("SlowdownProtoTestVictim", coords);
            brain = sEntMan.SpawnEntity("SlowdownProtoTestBrainOrgan", coords);
            torso = sEntMan.SpawnEntity("SlowdownProtoTestTorsoOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            // Brain first - resolving NerveSystem requires it, same ordering as TraumaBoneBreakTest.
            container.Insert(brain, organsContainer);
            container.Insert(torso, organsContainer);
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            sTrauma.TryCreateOrganDamageModifier(torso, FixedPoint2.New(20), victim, "TestDamage");
        });

        await pair.RunTicksSync(5);

        // If it throws/logs an unexpected error, the regression has returned.
        Assert.Pass();
    }
}
