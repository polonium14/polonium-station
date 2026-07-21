using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Administration.Systems;
using Content.Shared.Body;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

/// <summary>
/// User report: "rejuvenate does not bring back destroyed organs." A destroyed vital organ
/// (OrganSeverity.Destroyed) gets QueueDel'd and removed from the body entirely (see
/// TraumaSystem.Organs.cs's OnOrganSeverityChanged) - BodyRejuvenateSystem's existing
/// wound/bone/damage healing loop only touches organs still present, so a rejuvenated pawn kept
/// missing whatever vital organs it had lost. Fixed by having BodyRejuvenateSystem also read
/// InitialBodyComponent (the same category->EntProtoId manifest a body is built from at character
/// creation, see InitialBodySystem) and spawn+insert a fresh organ for any category that manifest
/// expects but the body currently lacks.
/// </summary>
[TestFixture]
[TestOf(typeof(Content.Shared._Shitmed.Body.BodyRejuvenateSystem))]
public sealed class RejuvenateRespawnsMissingOrganTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: RejuvOrganTestVictim
  components:
  - type: Body
  - type: InitialBody
    organs:
      Heart: RejuvOrganTestHeartOrgan

- type: entity
  id: RejuvOrganTestHeartOrgan
  components:
  - type: Organ
    category: Heart
  - type: OrganIntegrity
    integrityCap: 15
    integrityThresholds:
      Normal: 15
      Damaged: 6
      Destroyed: 0
";

    [Test]
    public async Task DestroyedOrganIsRespawnedOnRejuvenate()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sRejuvenate = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<RejuvenateSystem>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid victim = default;

        await server.WaitPost(() =>
        {
            victim = sEntMan.SpawnEntity("RejuvOrganTestVictim", coords);
        });

        await pair.RunTicksSync(5);

        EntityUid originalHeart = default;

        await server.WaitAssertion(() =>
        {
            var body = sEntMan.GetComponent<BodyComponent>(victim);
            Assert.That(body.Organs, Is.Not.Null);
            var hearts = body.Organs!.ContainedEntities
                .Where(o => sEntMan.GetComponent<OrganComponent>(o).Category == "Heart")
                .ToList();
            Assert.That(hearts, Has.Count.EqualTo(1), "InitialBodyComponent should have auto-spawned exactly one heart on map init.");
            originalHeart = hearts[0];
        });

        // Simulate the organ having been destroyed (same end state as OnOrganSeverityChanged's
        // QueueDel path: the organ entity is just gone).
        await server.WaitPost(() => sEntMan.DeleteEntity(originalHeart));
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var body = sEntMan.GetComponent<BodyComponent>(victim);
            var hearts = body.Organs!.ContainedEntities
                .Where(o => sEntMan.GetComponent<OrganComponent>(o).Category == "Heart")
                .ToList();
            Assert.That(hearts, Is.Empty, "Sanity check: the heart should actually be gone before rejuvenating.");
        });

        await server.WaitPost(() => sRejuvenate.PerformRejuvenate(victim));
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var body = sEntMan.GetComponent<BodyComponent>(victim);
            var hearts = body.Organs!.ContainedEntities
                .Where(o => sEntMan.GetComponent<OrganComponent>(o).Category == "Heart")
                .ToList();
            Assert.That(hearts, Has.Count.EqualTo(1), "Rejuvenate should have respawned the missing heart.");
            Assert.That(hearts[0], Is.Not.EqualTo(originalHeart), "The respawned heart should be a fresh entity, not the deleted one.");
        });
    }
}
