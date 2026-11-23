using Content.Client.Silicons.StationAi;
using Content.Shared._Polonium.Abductors;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;

namespace Content.Client._Polonium.Abductors;

/// <summary>
/// While the local player watches the station from an observation console,
/// only what the station's cameras see is shown.
/// </summary>
public sealed partial class AbductorObservationSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    // The camera overlay hooks a CVar in its constructor and never lets go of it, so it is reused rather than recreated.
    private StationAiOverlay? _cameraView;
    private AbductorOutsideViewOverlay? _outsideView;
    private AbductorReleaseMarkerOverlay? _marker;
    private Overlay? _activeView;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_player.LocalEntity is not { } local
            || !TryComp<AbductorObserverComponent>(local, out var observer)
            || !TryComp(observer.Eye, out TransformComponent? eye))
        {
            SetView(null);
            return;
        }

        _cameraView ??= new StationAiOverlay { StillStatic = true };
        _outsideView ??= new AbductorOutsideViewOverlay();
        _marker ??= new AbductorReleaseMarkerOverlay();

        _cameraView.GridOverride = eye.GridUid ?? EntityUid.Invalid;
        _outsideView.OwnGrid = Transform(local).GridUid;
        _marker.Point = observer.ReleasePoint;
        SetView(eye.GridUid != null && eye.GridUid == observer.StationGrid ? _cameraView : _outsideView);

        if (TryComp<SpriteComponent>(observer.Eye, out var sprite))
            _sprite.SetVisible((observer.Eye, sprite), true);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        SetView(null);
        _cameraView?.Dispose();
        _outsideView?.Dispose();
        _marker?.Dispose();
        _cameraView = null;
        _outsideView = null;
        _marker = null;
    }

    private void SetView(Overlay? view)
    {
        if (_activeView == view)
            return;

        if (_activeView != null)
            _overlays.RemoveOverlay(_activeView);
        else if (_marker != null)
            _overlays.AddOverlay(_marker);

        if (view != null)
            _overlays.AddOverlay(view);
        else if (_marker != null)
            _overlays.RemoveOverlay(_marker);

        _activeView = view;
    }
}
