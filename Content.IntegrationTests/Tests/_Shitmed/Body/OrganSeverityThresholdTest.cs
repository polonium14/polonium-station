using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
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
public sealed class OrganSeverityThresholdTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: OrganSeverityThresholdTestBody
  components:
  - type: Body

- type: entity
  id: OrganSeverityThresholdTestOrgan
  components:
  - type: Organ
    category: Torso
  - type: OrganIntegrity
    integrityCap: 200
    integrityThresholds:
      Normal: 200
      Damaged: 80
      Destroyed: 0
";

    [Test]
    public async Task DamagedOrganBelowNormalThresholdIsNotReportedAsNormal()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sTrauma = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<TraumaSystem>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid body = default;
        EntityUid organ = default;

        await server.WaitPost(() =>
        {
            body = sEntMan.SpawnEntity("OrganSeverityThresholdTestBody", coords);
            organ = sEntMan.SpawnEntity("OrganSeverityThresholdTestOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(body, BodyComponent.ContainerID);
            container.Insert(organ, organsContainer);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var integrity = sEntMan.GetComponent<OrganIntegrityComponent>(organ);
            Assert.That(integrity.OrganSeverity, Is.EqualTo(OrganSeverity.Normal), "Sanity check: an untouched, full-integrity organ should still read Normal.");
        });

        // Deal 100 points of organ-damage-modifier "damage" - IntegrityModifiers stores damage
        // magnitudes (see UpdateOrganIntegrity's own doc comment), so this should leave
        // 200-100=100/200 (50%) remaining: below the Normal threshold (200) but above the
        // Damaged threshold (80).
        await server.WaitPost(() =>
        {
            sTrauma.TryCreateOrganDamageModifier(organ, FixedPoint2.New(100), body, "TestDamage");
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var integrity = sEntMan.GetComponent<OrganIntegrityComponent>(organ);
            Assert.That(integrity.OrganIntegrity, Is.EqualTo(FixedPoint2.New(100)));
            Assert.That(integrity.OrganSeverity, Is.EqualTo(OrganSeverity.Damaged),
                "An organ at 100/200 (50%) integrity is below the Normal threshold (200) and should report Damaged, not Normal - this used to always report Normal regardless of actual damage.");
        });
    }
}
