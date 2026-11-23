using Content.Shared._Polonium.Radiation;
using Content.Shared.Radiation.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;

namespace Content.Client._Polonium.Radiation;

public sealed partial class RadiationNoiseSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPlayerManager _player = default!;

    [Dependency] private EntityQuery<IrradiatedVisionComponent> _visionQuery = default!;

    // How fast the noise follows a change of the radiation level, per second.
    private const float FadeRate = 1.5f;

    private RadiationNoiseOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new RadiationNoiseOverlay();
        _overlays.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlays.RemoveOverlay(_overlay);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var target = 0f;
        if (_player.LocalEntity is { } player && _visionQuery.TryComp(player, out var vision))
        {
            target = vision.Level switch
            {
                GeigerDangerLevel.Med => 0.35f,
                GeigerDangerLevel.High => 0.65f,
                GeigerDangerLevel.Extreme => 1f,
                _ => 0f,
            };
        }

        var step = frameTime * FadeRate;
        _overlay.Intensity = Math.Clamp(target, _overlay.Intensity - step, _overlay.Intensity + step);
    }
}
