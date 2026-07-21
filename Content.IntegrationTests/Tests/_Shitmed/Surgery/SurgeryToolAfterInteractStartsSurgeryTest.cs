using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared.Body;
using Content.Shared.Interaction;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Surgery;

[TestFixture]
public sealed class SurgeryToolAfterInteractStartsSurgeryTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: SurgeryAfterInteractTestVictim
  components:
  - type: Body
  - type: SurgeryTarget

- type: entity
  id: SurgeryAfterInteractTestUser

- type: entity
  id: SurgeryAfterInteractTestTool
  components:
  - type: SurgeryTool
";

    [Test]
    public async Task UsingSurgeryToolOnLyingDownPatientStartsSurgery()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid user = default;
        EntityUid victim = default;
        EntityUid tool = default;

        await server.WaitPost(() =>
        {
            // Distinct user/victim entities so this exercises the direct-tool-use path itself,
            // not the (separately gated) self-operate CVar branch.
            user = sEntMan.SpawnEntity("SurgeryAfterInteractTestUser", coords);
            victim = sEntMan.SpawnEntity("SurgeryAfterInteractTestVictim", coords);
            tool = sEntMan.SpawnEntity("SurgeryAfterInteractTestTool", coords);
        });

        await pair.RunTicksSync(5);

        AfterInteractEvent ev = default!;
        await server.WaitAssertion(() =>
        {
            var victimCoords = sEntMan.GetComponent<TransformComponent>(victim).Coordinates;
            ev = new AfterInteractEvent(user, tool, victim, victimCoords, canReach: true);
            sEntMan.EventBus.RaiseLocalEvent(tool, ev);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(ev.Handled, Is.True,
                "Using a surgery tool directly on a lying-down patient (no BuckleComponent, " +
                "so IsLyingDown trivially passes) should start surgery via OnAfterInteract, " +
                "the same way the explicit 'Start Surgery' verb already does.");
        });
    }

    [Test]
    public async Task UsingSurgeryToolOnNonSurgeryTargetDoesNotHandleTheInteraction()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid user = default;
        EntityUid notAPatient = default;
        EntityUid tool = default;

        await server.WaitPost(() =>
        {
            user = sEntMan.SpawnEntity("SurgeryAfterInteractTestUser", coords);
            // No SurgeryTargetComponent - e.g. a wall, a crate, some other item.
            notAPatient = sEntMan.SpawnEntity(null, coords);
            tool = sEntMan.SpawnEntity("SurgeryAfterInteractTestTool", coords);
        });

        await pair.RunTicksSync(5);

        AfterInteractEvent ev = default!;
        await server.WaitAssertion(() =>
        {
            var targetCoords = sEntMan.GetComponent<TransformComponent>(notAPatient).Coordinates;
            ev = new AfterInteractEvent(user, tool, notAPatient, targetCoords, canReach: true);
            sEntMan.EventBus.RaiseLocalEvent(tool, ev);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(ev.Handled, Is.False,
                "OnAfterInteract should ignore targets without SurgeryTargetComponent entirely, " +
                "leaving the interaction unhandled for anything else that might want it.");
        });
    }
}
