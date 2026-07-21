using System.Numerics;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.RCD;
using Content.Shared.RCD.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Construction;

/// <summary>
/// Live playtest report: "RPD sometimes ignores rotation of the construction ghost when
/// placing pipes." Root cause: RCDSystem.OnDoAfter/FinalizeRCDOperation used to spawn the
/// constructed entity facing the Direction value that got snapshotted into RCDDoAfterEvent
/// back when the (networked, DoAfter-delayed) placement started, rather than the RCD
/// component's live ConstructionDirection at the moment placement actually completes. Most
/// RPD pipe entries have delay: 0 (a razor-thin window, hence "sometimes"), but several other
/// RCD-placeable RcdRotation.User entries (WindowDirectional: delay 1s, ReinforcedWindow:
/// delay 3s) have a real, deliberately-testable window - this test uses WindowDirectional to
/// deterministically reproduce rotating the ghost after a placement has already been queued,
/// which used to place at the stale pre-rotate direction. Fixed by reading
/// component.ConstructionDirection directly in FinalizeRCDOperation instead of the DoAfter
/// event's own frozen copy - the same fix benefits every RcdRotation.User entry (pipes, pumps,
/// valves, vents, windows), not just pipes specifically, since they all go through this one
/// shared code path.
/// </summary>
public sealed class RCDRotationRaceTest : InteractionTest
{
    private static readonly EntProtoId RCDProtoId = "RCD";
    private static readonly ProtoId<RCDPrototype> RCDSettingWindow = "WindowDirectional";

    [Test]
    public async Task RotatingGhostAfterPlacementStartsUsesTheNewRotation()
    {
        var pNorth = new EntityCoordinates(SPlayer, new Vector2(0, 1));
        pNorth = Transform.WithEntityId(pNorth, MapData.Grid);

        await SetTile(PlatingRCD, SEntMan.GetNetCoordinates(pNorth), MapData.Grid);

        Assert.That(ProtoMan.TryIndex(RCDSettingWindow, out var settingWindow), $"RCDPrototype not found: {RCDSettingWindow}.");
        Assert.That(settingWindow.Delay, Is.GreaterThan(0), "This test relies on a real DoAfter window to inject a mid-placement rotation change.");

        var rcd = await PlaceInHands(RCDProtoId);

        await SetRcdProto(rcd, RCDSettingWindow);

        // Start the placement facing South - the same client->server networked event the real
        // ghost-rotate keybind sends (RCDSystem.OnRCDconstructionGhostRotationEvent).
        await SendGhostRotation(rcd, Direction.South);

        // awaitDoAfters: false - we need control back *before* the DoAfter finishes, to inject
        // the mid-placement rotation change below.
        await Interact(null, pNorth, awaitDoAfters: false);

        // Rotate the ghost to East *after* the DoAfter has already been queued, but before it
        // completes - exactly what a player does when they spin the ghost mid-placement.
        await SendGhostRotation(rcd, Direction.East);

        await RunSeconds(settingWindow.Delay + 1);

        await AssertEntityLookup((settingWindow.Prototype, 1));

        var windowUid = await FindEntity(settingWindow.Prototype);
        var windowRotation = SEntMan.GetComponent<TransformComponent>(windowUid).LocalRotation;

        Assert.That(windowRotation, Is.EqualTo(Direction.East.ToAngle()),
            "The window should have spawned facing the LATER rotation (East, set after placement started), not the direction that was current when the DoAfter first began (South) - this is exactly the reported 'RPD sometimes ignores rotation of the construction ghost' bug.");
    }

    private async Task SetRcdProto(NetEntity rcd, ProtoId<RCDPrototype> protoId)
    {
        await UseInHand();
        await RunTicks(3);
        Assert.That(IsUiOpen(RcdUiKey.Key), Is.True, "RCD UI was not opened when using the RCD while holding it.");
        await SendBui(RcdUiKey.Key, new RCDSystemMessage(protoId), rcd);
        await CloseBui(RcdUiKey.Key, rcd);
        Assert.That(IsUiOpen(RcdUiKey.Key), Is.False, "RCD UI is still open.");
    }

    private async Task SendGhostRotation(NetEntity rcd, Direction direction)
    {
        await Client.WaitPost(() => CEntMan.RaisePredictiveEvent(new RCDConstructionGhostRotationEvent(rcd, direction)));
        await RunTicks(5);
    }
}
