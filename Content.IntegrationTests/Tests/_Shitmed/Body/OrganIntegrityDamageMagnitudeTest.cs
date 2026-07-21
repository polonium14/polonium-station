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
public sealed class OrganIntegrityDamageMagnitudeTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: OrganDamageMagnitudeTestBody
  components:
  - type: Body

- type: entity
  id: OrganDamageMagnitudeTestOrgan
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
    public async Task SevereHitDoesNotClampIntegrityUpToFullCap()
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
            body = sEntMan.SpawnEntity("OrganDamageMagnitudeTestBody", coords);
            organ = sEntMan.SpawnEntity("OrganDamageMagnitudeTestOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(body, BodyComponent.ContainerID);
            container.Insert(organ, organsContainer);
        });

        await pair.RunTicksSync(5);

        // A severity of 20, well above this organ's IntegrityCap of 15 - a completely realistic
        // single-wound severity (OrganDamage traumas only roll above severity 15 in the first
        // place). This crosses into OrganSeverity.Destroyed, which queues the organ for deletion
        // (TraumaSystem.Organs.cs's OnOrganSeverityChanged) - captured into locals inside the
        // same WaitPost, right after the call, so the assertions below don't touch the organ
        // entity again afterward.
        FixedPoint2 integrityAfterHit = default;
        OrganSeverity severityAfterHit = default;

        await server.WaitPost(() =>
        {
            sTrauma.TryCreateOrganDamageModifier(organ, FixedPoint2.New(20), body, "TestDamage");
            var integrity = sEntMan.GetComponent<OrganIntegrityComponent>(organ);
            integrityAfterHit = integrity.OrganIntegrity;
            severityAfterHit = integrity.OrganSeverity;
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(integrityAfterHit, Is.EqualTo(FixedPoint2.Zero),
                "20 damage against a 15-cap organ should floor integrity at zero (fully destroyed), not clamp UP to the 15 cap and show the organ at 100% health.");
            Assert.That(severityAfterHit, Is.EqualTo(OrganSeverity.Destroyed));
        });
    }
}
