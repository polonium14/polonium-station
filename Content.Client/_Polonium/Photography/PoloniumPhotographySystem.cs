using Content.Client.Items;
using Content.Shared._Polonium.Photography;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Map;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client._Polonium.Photography;

/// <summary>Client half: on a server capture request, hands the target to the capture control, which renders the player's view, crops around the target, grades + packs to RGB565, and sends it up.</summary>
public sealed partial class PoloniumPhotographySystem : SharedPoloniumPhotographySystem
{
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private PhotographyCaptureControl _control = default!;

    public override void Initialize()
    {
        base.Initialize();

        _control = new PhotographyCaptureControl(_clyde, PackAndSubmit);
        _ui.RootControl.AddChild(_control);

        SubscribeNetworkEvent<RequestPhotoCaptureEvent>(OnCaptureRequested);

        Subs.ItemStatus<PoloniumCameraComponent>(ent => new CameraStatusControl(ent.Owner, EntityManager));
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _ui.RootControl.RemoveChild(_control);
        _control.Dispose();
    }

    private void OnCaptureRequested(RequestPhotoCaptureEvent ev)
    {
        var entCoords = GetCoordinates(ev.Coordinates);
        if (!entCoords.IsValid(EntityManager))
            return;

        var mapCoords = _transform.ToMapCoordinates(entCoords);
        if (mapCoords.MapId == MapId.Nullspace)
            return;

        _control.Enqueue(ev.CaptureId, mapCoords, ev.Flash);
    }

    /// <summary>Pack the read-back pixels to RGB565 and send them up. Runs inside the capture control's Draw (GPU thread) via the read-back callback.</summary>
    private void PackAndSubmit(int captureId, Image<Rgba32> image)
    {
        var packed = PhotoCodec.ToRgb565(image);
        RaiseNetworkEvent(new SubmitPhotoEvent(captureId, packed));
    }
}
