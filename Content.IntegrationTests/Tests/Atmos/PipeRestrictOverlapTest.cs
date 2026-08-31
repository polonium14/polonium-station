using System.Numerics;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Atmos;
using Content.Shared.Construction.Components;
using Content.Shared.Construction.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Atmos;

/// <summary>
/// Live playtest report: "placing a pipe using RPD over an existing one to cross them does not
/// anchor the second pipe - user has to anchor them manually using a wrench." Investigated
/// alongside a separate report ("RPD sometimes ignores rotation of the construction ghost") -
/// PipeRestrictOverlapSystem.OnAnchorStateChanged (Content.Shared/Atmos/EntitySystems/
/// PipeRestrictOverlapSystem.cs) auto-unanchors a just-anchored pipe if CheckOverlap finds a
/// real node conflict (same AtmosPipeLayer AND an intersecting PipeDirection bit) with another
/// anchored pipe on the same tile - by design, so two pipes that are NOT meant to connect
/// (e.g. a proper perpendicular crossing) should NOT trigger it. The leading theory was that
/// the "doesn't anchor" report was entirely downstream of the separate rotation bug (a
/// mis-rotated second pipe would genuinely conflict, and the safety-unanchor would correctly
/// but confusingly kick in) rather than a second, independent bug in the overlap check itself.
/// This test verifies that theory directly: two straight pipes on the same tile, same layer,
/// rotated 90 degrees apart (a textbook non-connecting crossing) should both end up anchored.
/// If this test passes, the RCDSystem rotation fix (RCDRotationRaceTest) is the complete fix
/// for both reports; no separate PipeRestrictOverlapSystem change is needed.
/// </summary>
public sealed class PipeRestrictOverlapTest : InteractionTest
{
    private static readonly EntProtoId StraightPipe = "GasPipeStraight";

    [Test]
    public async Task PerpendicularCrossingPipesBothStayAnchored()
    {
        var coords = new EntityCoordinates(SPlayer, new Vector2(0, 1));
        coords = Transform.WithEntityId(coords, MapData.Grid);
        var netCoords = SEntMan.GetNetCoordinates(coords);

        await SetTile(PlatingRCD, netCoords, MapData.Grid);

        EntityUid pipeA = default;
        EntityUid pipeB = default;

        await Server.WaitPost(() =>
        {
            var sCoords = SEntMan.GetCoordinates(netCoords);

            // Both pipes spawn with their rotation applied atomically via SpawnAttachedTo,
            // matching RCDSystem's own fixed spawn call - straight pipes anchor immediately on
            // spawn (Transform.anchored: true is a prototype default), so
            // PipeRestrictOverlapSystem's conflict check runs synchronously as part of
            // spawning. Rotating as a separate follow-up call (the pre-fix pattern) would make
            // that check run against the wrong, still-unrotated transform. Uses
            // SpawnAttachedTo (EntityCoordinates, grid-parented), NOT the MapCoordinates+
            // rotation Spawn overload - that overload's rotation is WORLD rotation, which is
            // wrong on any grid not itself at world rotation 0.

            // Pipe A: unrotated straight pipe (North-South, per PipeDirection.Longitudinal).
            pipeA = SEntMan.SpawnAttachedTo(StraightPipe, sCoords, rotation: Angle.Zero);

            // Pipe B: same straight pipe, rotated 90 degrees to run East-West - a textbook
            // non-connecting crossing, sharing the tile with pipe A but no overlapping ports.
            pipeB = SEntMan.SpawnAttachedTo(StraightPipe, sCoords, rotation: Direction.East.ToAngle());
        });

        await RunTicks(10);

        await Server.WaitAssertion(() =>
        {
            var xformA = SEntMan.GetComponent<TransformComponent>(pipeA);
            var xformB = SEntMan.GetComponent<TransformComponent>(pipeB);

            Assert.That(xformA.Anchored, Is.True,
                "Pipe A (unrotated, North-South) should stay anchored - nothing else was on the tile when it anchored.");
            Assert.That(xformB.Anchored, Is.True,
                "Pipe B (rotated 90 degrees, East-West) should ALSO stay anchored - it doesn't share any port direction with pipe A, so PipeRestrictOverlapSystem's conflict check should not fire. If this fails, the 'crossing pipe doesn't auto-anchor' report has a second, independent cause beyond the RCD rotation bug.");
        });
    }

    [TestCase("GasVentPump", "GasVentPumpAlt1")]       // device over device, different layers
    [TestCase("GasPressurePump", "GasPressurePumpAlt1")]
    [TestCase("GasPipeStraight", "GasPipeStraightAlt1")] // pipe over pipe, different layers
    [TestCase("GasPipeStraight", "GasVentPump")]        // pipe under device, same layer
    [TestCase("GasVentPump", "GasPipeStraight")]        // device over pipe, same layer
    public async Task CanWrenchAnchorOntoOccupiedTile(string firstId, string secondId)
    {
        var coords = new EntityCoordinates(SPlayer, new Vector2(0, 1));
        coords = Transform.WithEntityId(coords, MapData.Grid);
        var netCoords = SEntMan.GetNetCoordinates(coords);

        await SetTile(PlatingRCD, netCoords, MapData.Grid);

        EntityUid first = default;
        EntityUid second = default;

        await Server.WaitPost(() =>
        {
            var sCoords = SEntMan.GetCoordinates(netCoords);
            first = SEntMan.SpawnAttachedTo(firstId, sCoords, rotation: Angle.Zero);
            second = SEntMan.SpawnAttachedTo(secondId, sCoords, rotation: Angle.Zero);

            SEntMan.System<SharedTransformSystem>()
                .Unanchor(second, SEntMan.GetComponent<TransformComponent>(second));
        });

        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var anchorable = SEntMan.System<AnchorableSystem>();
            var xform = SEntMan.GetComponent<TransformComponent>(second);

            Assert.That(SEntMan.GetComponent<TransformComponent>(first).Anchored, Is.True,
                $"{firstId} should have stayed anchored on an empty tile.");

            var attempt = new AnchorAttemptEvent(SPlayer, SPlayer);
            SEntMan.EventBus.RaiseLocalEvent(second, attempt);

            Assert.Multiple(() =>
            {
                Assert.That(attempt.Cancelled, Is.False,
                    $"PipeRestrictOverlap blocked anchoring {secondId} over {firstId}.");
                Assert.That(anchorable.CanAnchorAt(second, xform.Coordinates), Is.True,
                    $"TileFree blocked anchoring {secondId} over {firstId}.");
                Assert.That(anchorable.AnyUnstackable(second, xform.Coordinates), Is.False,
                    $"The Unstackable tag blocked anchoring {secondId} over {firstId}.");
            });
        });
    }

    [TestCase("GasVentPump", "GasVentPump")]             // two devices, same layer
    [TestCase("GasPipeStraight", "GasPipeStraight")]     // two pipes, same layer and direction
    [TestCase("GasVentPump", "GasPressurePump")]         // different devices, same layer
    public async Task CannotWrenchAnchorOntoSameLayer(string firstId, string secondId)
    {
        var coords = new EntityCoordinates(SPlayer, new Vector2(0, 1));
        coords = Transform.WithEntityId(coords, MapData.Grid);
        var netCoords = SEntMan.GetNetCoordinates(coords);

        await SetTile(PlatingRCD, netCoords, MapData.Grid);

        EntityUid first = default;
        EntityUid second = default;

        await Server.WaitPost(() =>
        {
            var sCoords = SEntMan.GetCoordinates(netCoords);
            first = SEntMan.SpawnAttachedTo(firstId, sCoords, rotation: Angle.Zero);
            second = SEntMan.SpawnAttachedTo(secondId, sCoords, rotation: Angle.Zero);
            SEntMan.System<SharedTransformSystem>()
                .Unanchor(second, SEntMan.GetComponent<TransformComponent>(second));
        });

        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var anchorable = SEntMan.System<AnchorableSystem>();
            var xform = SEntMan.GetComponent<TransformComponent>(second);

            var attempt = new AnchorAttemptEvent(SPlayer, SPlayer);
            SEntMan.EventBus.RaiseLocalEvent(second, attempt);

            Assert.That(attempt.Cancelled || anchorable.AnyUnstackable(second, xform.Coordinates), Is.True,
                $"{secondId} was allowed to anchor over {firstId} on the same pipe layer - the layer-aware gates have been loosened too far.");
        });
    }
}
