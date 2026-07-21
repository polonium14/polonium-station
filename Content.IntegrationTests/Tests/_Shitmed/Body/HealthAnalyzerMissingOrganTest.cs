using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.Medical;
using Content.Shared.Body;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

/// <summary>
/// User request: "when an organ is missing report it in the body tab and in the organs tab."
/// HealthAnalyzerSystem.FetchMissingOrgansData diffs BodyComponent.ExpectedOrgans (see
/// ExpectedOrgansManifestTest for that primitive) against currently-present organ categories -
/// follows HealthAnalyzerUnfinishedSurgeryTest's exact pattern (private method invoked via
/// reflection, not the full BUI/scan flow).
///
/// Covers both ways an organ can go missing: surgical removal (relocated out of the container,
/// entity still alive) and outright deletion - and confirms a limb (never recorded in
/// ExpectedOrgans to begin with) never shows up here.
/// </summary>
[TestFixture]
[TestOf(typeof(HealthAnalyzerSystem))]
public sealed class HealthAnalyzerMissingOrganTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: MissingOrganTestVictim
  components:
  - type: Body

- type: entity
  id: MissingOrganTestHeartOrgan
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
  id: MissingOrganTestArmOrgan
  components:
  - type: Organ
    category: ArmLeft
";

    [Test]
    public async Task RemovedOrDeletedVitalOrganIsReportedMissingButLimbNeverIs()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sHealthAnalyzer = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<HealthAnalyzerSystem>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid victim = default;
        EntityUid heart = default;
        EntityUid arm = default;
        BodyComponent bodyComp = default!;

        await server.WaitPost(() =>
        {
            victim = sEntMan.SpawnEntity("MissingOrganTestVictim", coords);
            heart = sEntMan.SpawnEntity("MissingOrganTestHeartOrgan", coords);
            arm = sEntMan.SpawnEntity("MissingOrganTestArmOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(heart, organsContainer);
            container.Insert(arm, organsContainer);

            bodyComp = sEntMan.GetComponent<BodyComponent>(victim);
        });

        await pair.RunTicksSync(5);

        var fetchMethod = typeof(HealthAnalyzerSystem).GetMethod("FetchMissingOrgansData", BindingFlags.Instance | BindingFlags.NonPublic)!;

        await server.WaitAssertion(() =>
        {
            var before = (List<ProtoId<OrganCategoryPrototype>>) fetchMethod.Invoke(sHealthAnalyzer, new object[] { bodyComp })!;
            Assert.That(before, Is.Empty, "Nothing should show as missing while the heart is still present.");
        });

        // Surgical removal: taken out of the container, entity still alive.
        await server.WaitPost(() =>
        {
            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Remove(heart, organsContainer);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var afterRemoval = (List<ProtoId<OrganCategoryPrototype>>) fetchMethod.Invoke(sHealthAnalyzer, new object[] { bodyComp })!;
            Assert.That(afterRemoval, Does.Contain(new ProtoId<OrganCategoryPrototype>("Heart")),
                "A heart taken out of the body should be reported missing, whether or not the organ entity itself still exists.");
            Assert.That(afterRemoval, Has.Count.EqualTo(1), "The limb was never recorded as expected, so it must never appear here.");
        });

        // Outright deletion should report the same way as a plain removal.
        await server.WaitPost(() => sEntMan.DeleteEntity(heart));
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var afterDeletion = (List<ProtoId<OrganCategoryPrototype>>) fetchMethod.Invoke(sHealthAnalyzer, new object[] { bodyComp })!;
            Assert.That(afterDeletion, Does.Contain(new ProtoId<OrganCategoryPrototype>("Heart")),
                "Deleting the organ entity outright must also leave it reported as missing.");
        });
    }
}
