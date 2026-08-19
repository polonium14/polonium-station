using System.Numerics;
using Content.Shared._Polonium.Photography;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client._Polonium.Photography;

/// <summary>Screen-space viewfinder: via <c>camera_viewfinder.swsl</c>, dims + blurs everything except the sharp window around the shot's target. <see cref="CameraViewfinderSystem"/> updates the window each frame; this overlay feeds it to the shader.</summary>
public sealed class CameraViewfinderOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> ShaderProto = "PoloniumCameraViewfinder";

    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private readonly ShaderInstance _shader;

    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public Vector2 CenterWorld;
    public Color BorderColor = Color.Black;

    public float Dim = 0.15f;
    public float Blur = 4.5f;
    public float BorderWidth = 1.5f;

    public CameraViewfinderOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _proto.Index(ShaderProto).InstanceUnique();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return args.Viewport.Eye == _eye.CurrentEye;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        // Window rect in viewport render-target pixels (matches the shader's px).
        var vp = args.Viewport;
        var centerPx = vp.WorldToLocal(CenterWorld);
        var pxPerTile = (vp.WorldToLocal(CenterWorld + new Vector2(1f, 0f)) - centerPx).Length();
        var halfPx = PhotographyConstants.TilesPerSide / 2f * pxPerTile;

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("CENTER_PX", centerPx);
        _shader.SetParameter("HALF_PX", new Vector2(halfPx, halfPx));
        _shader.SetParameter("DIM", Dim);
        _shader.SetParameter("BLUR", Blur);
        _shader.SetParameter("BORDER_PX", BorderWidth);
        _shader.SetParameter("BORDER_COLOR", new Vector3(BorderColor.R, BorderColor.G, BorderColor.B));

        var handle = args.WorldHandle;
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
