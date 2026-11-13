using Robust.Client.Graphics;

namespace Content.Client._Starlight.Light;

/// <summary>
/// Adds the <see cref="ScreenPostProcessOverlay"/>, which checks its own cvars every frame.
/// </summary>
public sealed partial class ScreenPostProcessSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay.AddOverlay(new ScreenPostProcessOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<ScreenPostProcessOverlay>();
    }
}
