using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Radiation;

/// <summary>
/// White speckles over the game view, like radiation hitting a camera sensor.
/// </summary>
public sealed partial class RadiationNoiseOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> ShaderProto = "PoloniumRadiationNoise";

    // With reduced motion the speckles move a few times a second instead of every frame.
    private const float ReducedMotionRate = 3f;

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private readonly ShaderInstance _shader;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public float Intensity;

    public RadiationNoiseOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _proto.Index(ShaderProto).InstanceUnique();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return Intensity > 0.001f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var time = (float) (_timing.RealTime.TotalSeconds % 1000);
        if (_cfg.GetCVar(CCVars.ReducedMotion))
            time = MathF.Floor(time * ReducedMotionRate) / ReducedMotionRate;

        _shader.SetParameter("intensity", Intensity);
        _shader.SetParameter("seed", time);

        var handle = args.WorldHandle;
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
