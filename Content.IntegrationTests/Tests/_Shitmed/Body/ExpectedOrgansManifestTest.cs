using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Body;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

/// <summary>
/// BodyComponent.ExpectedOrgans is the detection primitive for HealthAnalyzer's "missing organ"
/// report - it's populated by BodySystem.OnBodyEntInserted reacting to the same container-insert
/// pipeline every organ (including ones spawned programmatically at character creation, not via
/// a YAML body_organs fill list) goes through. Confirms that pipeline actually reaches it before
/// building anything on top: a vital organ (has OrganIntegrity) must be recorded, a limb organ
/// (no OrganIntegrity) must not be, matching the "don't double-report dismemberment" scoping
/// decision in BodySystem.cs.
/// </summary>
[TestFixture]
[TestOf(typeof(BodySystem))]
public sealed class ExpectedOrgansManifestTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: ExpectedOrgansTestVictim
  components:
  - type: Body

- type: entity
  id: ExpectedOrgansTestHeartOrgan
  components:
  - type: Organ
    category: Heart
  - type: OrganIntegrity
    integrityCap: 15
    integrityThresholds:
      Normal: 15
      Damaged: 6
      Destroyed: 0

- type: entity
  id: ExpectedOrgansTestArmOrgan
  components:
  - type: Organ
    category: ArmLeft
";

    [Test]
    public async Task VitalOrganInsertedIntoBodyIsRecordedInExpectedOrgansButLimbIsNot()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid victim = default;
        EntityUid heart = default;
        EntityUid arm = default;

        await server.WaitPost(() =>
        {
            victim = sEntMan.SpawnEntity("ExpectedOrgansTestVictim", coords);
            heart = sEntMan.SpawnEntity("ExpectedOrgansTestHeartOrgan", coords);
            arm = sEntMan.SpawnEntity("ExpectedOrgansTestArmOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(heart, organsContainer);
            container.Insert(arm, organsContainer);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var body = sEntMan.GetComponent<BodyComponent>(victim);
            Assert.That(body.ExpectedOrgans, Does.Contain(new Robust.Shared.Prototypes.ProtoId<OrganCategoryPrototype>("Heart")),
                "Inserting a vital organ (has OrganIntegrity) must record its category as expected.");
            Assert.That(body.ExpectedOrgans, Does.Not.Contain(new Robust.Shared.Prototypes.ProtoId<OrganCategoryPrototype>("ArmLeft")),
                "Limbs (no OrganIntegrity) must not be recorded - dismemberment already has its own visible state.");
        });
    }
}
