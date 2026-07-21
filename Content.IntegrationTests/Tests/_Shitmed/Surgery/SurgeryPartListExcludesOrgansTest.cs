using System.Collections.Generic;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared.Body;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Shitmed.Surgery;

[TestFixture]
public sealed class SurgeryPartListExcludesOrgansTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: PartListTestVictim
  components:
  - type: Body
  - type: Damageable
  - type: Injurable
  - type: SurgeryTarget

- type: entity
  id: PartListTestTorsoOrgan
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
  id: PartListTestHeartOrgan
  components:
  - type: Organ
    category: Heart
  - type: OrganIntegrity
    integrityCap: 17
    integrityThresholds:
      Normal: 17
      Damaged: 9
      Destroyed: 0
";

    [Test]
    public async Task RefreshUiOmitsInternalOrgansButKeepsExternalParts()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sSurgery = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SurgerySystem>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(System.Numerics.Vector2.Zero, map.MapId);

        EntityUid victim = default;
        EntityUid torso = default;
        EntityUid heart = default;

        await server.WaitPost(() =>
        {
            victim = sEntMan.SpawnEntity("PartListTestVictim", coords);
            torso = sEntMan.SpawnEntity("PartListTestTorsoOrgan", coords);
            heart = sEntMan.SpawnEntity("PartListTestHeartOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(torso, organsContainer);
            container.Insert(heart, organsContainer);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var refreshUi = typeof(SurgerySystem).GetMethod("RefreshUI", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(refreshUi, Is.Not.Null, "RefreshUI should still exist as a protected instance method on SurgerySystem.");
            refreshUi!.Invoke(sSurgery, new object[] { victim });

            var surgeriesField = typeof(SurgerySystem).GetField("_surgeries", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(surgeriesField, Is.Not.Null);
            var surgeries = (Dictionary<NetEntity, List<EntProtoId>>)surgeriesField!.GetValue(sSurgery)!;

            var torsoNet = sEntMan.GetNetEntity(torso);
            var heartNet = sEntMan.GetNetEntity(heart);

            Assert.That(surgeries.ContainsKey(torsoNet), Is.True,
                "The torso - a real external body part - should still be a top-level entry.");
            Assert.That(surgeries.ContainsKey(heartNet), Is.False,
                "The heart - an internal organ - should NOT be its own top-level entry anymore; heart surgery is reachable through the torso instead.");
        });
    }
}
