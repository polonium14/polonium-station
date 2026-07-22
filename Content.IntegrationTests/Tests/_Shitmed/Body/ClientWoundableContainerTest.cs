using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

[TestFixture]
public sealed class ClientWoundableContainerTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: ClientWoundableTestVictim
  components:
  - type: Body
  - type: Damageable
  - type: Injurable
  - type: StandingState
  - type: MovementSpeedModifier

- type: entity
  id: ClientWoundableTestTorso
  components:
  - type: Organ
    category: Torso
  - type: Damageable
  - type: Injurable
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
  id: ClientWoundableTestLegLeft
  components:
  - type: Organ
    category: LegLeft
  - type: Damageable
  - type: Injurable
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
  id: ClientWoundableTestLegRight
  components:
  - type: Organ
    category: LegRight
  - type: Damageable
  - type: Injurable
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
    public async Task ClientResolvesBoneContainersAndSpeedRefreshDoesNotDownMob()
    {
        var pair = Pair;
        var server = pair.Server;
        var client = pair.Client;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var cEntMan = client.ResolveDependency<IEntityManager>();

        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid victim = default;
        EntityUid legLeft = default;
        EntityUid legRight = default;

        await server.WaitPost(() =>
        {
            victim = sEntMan.SpawnEntity("ClientWoundableTestVictim", coords);
            var torso = sEntMan.SpawnEntity("ClientWoundableTestTorso", coords);
            legLeft = sEntMan.SpawnEntity("ClientWoundableTestLegLeft", coords);
            legRight = sEntMan.SpawnEntity("ClientWoundableTestLegRight", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(torso, organsContainer);
            container.Insert(legLeft, organsContainer);
            container.Insert(legRight, organsContainer);
        });

        await pair.RunTicksSync(5);

        var clientVictim = cEntMan.GetEntity(sEntMan.GetNetEntity(victim));
        var clientLegLeft = cEntMan.GetEntity(sEntMan.GetNetEntity(legLeft));
        var clientLegRight = cEntMan.GetEntity(sEntMan.GetNetEntity(legRight));

        await client.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var leg in new[] { clientLegLeft, clientLegRight })
                {
                    var woundable = cEntMan.GetComponent<WoundableComponent>(leg);
                    Assert.That(woundable.Wounds, Is.Not.Null,
                        "The client should resolve a replicated organ's Wounds container from networked state.");
                    Assert.That(woundable.Bone, Is.Not.Null,
                        "The client should resolve a replicated organ's Bone container from networked state.");

                    var hasBone = false;
                    foreach (var contained in woundable.Bone!.ContainedEntities)
                    {
                        hasBone |= cEntMan.HasComponent<BoneComponent>(contained);
                    }

                    Assert.That(hasBone,
                        "The server-spawned bone entity should be visible inside the client's Bone container.");
                }
            });
        });

        await client.WaitPost(() =>
        {
            cEntMan.System<MovementSpeedModifierSystem>().RefreshMovementSpeedModifiers(clientVictim);
        });

        await client.WaitAssertion(() =>
        {
            Assert.That(cEntMan.System<StandingStateSystem>().IsDown(clientVictim), Is.False,
                "A movement-speed refresh on a healthy mob must not down it on the client.");
        });

        await server.WaitPost(() =>
        {
            sEntMan.System<MovementSpeedModifierSystem>().RefreshMovementSpeedModifiers(victim);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(sEntMan.System<StandingStateSystem>().IsDown(victim), Is.False,
                "A movement-speed refresh on a healthy mob must not down it on the server.");
        });
    }
}
