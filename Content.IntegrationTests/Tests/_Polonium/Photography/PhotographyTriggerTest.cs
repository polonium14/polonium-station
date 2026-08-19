using System.Numerics;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._Polonium.Photography;
using Content.Shared._Polonium.Photography;
using Content.Shared.Item.ItemToggle;

namespace Content.IntegrationTests.Tests._Polonium.Photography;

/// <summary>
/// Exercises the server half end-to-end (minus the client render): left-click with the
/// camera must reach our trigger and issue exactly one capture token to the shooter's
/// session, flash on or off. This is the wiring (camera prototype, interaction, our system,
/// session-targeted token) that can't be seen by reading the code. The flash-on case also
/// drives the world-burst path (spawn light + area blind) so a crash there is caught.
/// </summary>
[TestOf(typeof(PoloniumPhotographySystem))]
public sealed class PhotographyTriggerTest : InteractionTest
{
    private const string CameraProto = "PoloniumCamera";
    private const string TargetProto = "Wrench";

    [Test]
    public async Task ShutterIssuesOneCaptureToken()
    {
        var sys = SEntMan.System<PoloniumPhotographySystem>();

        await SpawnTarget(TargetProto);
        await PlaceInHands(CameraProto);

        int before = default;
        await Server.WaitPost(() => before = sys.PendingCount);
        Assert.That(before, Is.Zero);

        // Left-click / ranged-interact the target with the camera in hand -> capture token.
        await Interact();

        int after = default;
        await Server.WaitPost(() => after = sys.PendingCount);
        Assert.That(after, Is.EqualTo(1), "Firing the shutter must issue exactly one capture token.");
    }

    [Test]
    public async Task FlashOnStillCapturesAndFiresBurst()
    {
        var sys = SEntMan.System<PoloniumPhotographySystem>();
        var toggle = SEntMan.System<ItemToggleSystem>();

        await SpawnTarget(TargetProto);
        var cam = await PlaceInHands(CameraProto);

        // Arm the flash, then shoot. This runs the world-burst path (light + area blind);
        // the assertion is that a token is still issued and nothing threw.
        await Server.WaitPost(() => toggle.TrySetActive(ToServer(cam), true));
        await RunTicks(1);

        await Interact();

        int after = default;
        await Server.WaitPost(() => after = sys.PendingCount);
        Assert.That(after, Is.EqualTo(1), "A flash-on shot still issues exactly one capture token.");
    }

    [Test]
    public async Task OutOfRangeTargetTakesNoPhoto()
    {
        var sys = SEntMan.System<PoloniumPhotographySystem>();

        await PlaceInHands(CameraProto);

        // A click far beyond PhotoMaxRange fails the line-of-sight/range gate, so no
        // capture token is issued. (Wall occlusion at closer range is a live-client check.)
        var far = MapData.GridCoords.Offset(new Vector2(PhotographyConstants.PhotoMaxRange + 5f, 0.5f));
        await Interact(null, far);

        int after = default;
        await Server.WaitPost(() => after = sys.PendingCount);
        Assert.That(after, Is.Zero, "A target beyond view range yields no photo.");
    }
}
