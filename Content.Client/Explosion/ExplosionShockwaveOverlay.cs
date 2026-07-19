using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Explosion;

/// <summary>
/// Full-screen post-process overlay that renders expanding distortion rings
/// for explosion shockwaves, synced to server time.
/// Nuclear-grade waves additionally render a blinding white flash on top.
/// </summary>
public sealed class ExplosionShockwaveOverlay : Overlay
{
    private readonly IGameTiming _timing;
    private readonly ExplosionShockwaveSystem _system;
    private readonly ShaderInstance _shader;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    /// <summary>
    /// Must match the uniform array lengths in shockwave.swsl (shader was rewritten by chatGPT and was not verified completely)
    /// </summary>
    private const int MaxRings = 8;
    private const string ShaderID = "Shockwave";

    private readonly Vector2[] _centers = new Vector2[MaxRings];
    private readonly float[] _radii = new float[MaxRings];
    private readonly float[] _thicknesses = new float[MaxRings];
    private readonly float[] _warpStrengths = new float[MaxRings];
    private int _count;

    public ExplosionShockwaveOverlay(ExplosionShockwaveSystem system, IPrototypeManager protoManager, IGameTiming timing)
    {
        _system = system;
        _timing = timing;
        _shader = protoManager.Index<ShaderPrototype>(ShaderID).Instance().Duplicate();
        ZIndex = 102;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (args.Viewport.Eye == null || _system.ActiveWaves.Count == 0)
            return false;

        var viewport = args.Viewport;
        var now = _timing.CurTime.TotalSeconds;
        var ppm = EyeManager.PixelsPerMeter;

        _count = 0;
        foreach (var wave in _system.ActiveWaves)
        {
            if (_count >= MaxRings)
                break;

            if (wave.MapId != args.MapId)
                continue;

            var elapsed = (float) Math.Max(0, now - wave.ServerStartSeconds);
            var t = Math.Clamp(elapsed / wave.DurationSeconds, 0f, 1f);

            var radiusTiles = t * wave.MaxRadiusTiles;
            if (radiusTiles <= 0f)
                continue;

            var centerLocal = viewport.WorldToLocal(wave.EpicenterWorld);
            centerLocal.Y = viewport.Size.Y - centerLocal.Y;

            var radiusPx = radiusTiles * ppm;

            var baseThickness = wave.Flash
                ? Math.Clamp(wave.MaxRadiusTiles * 0.25f, 4f, 16f)
                : Math.Clamp(wave.MaxRadiusTiles * 0.12f, 2f, 8f);
            var thicknessPx = baseThickness * ppm;

            var fadeIn = Math.Clamp(t * 5f, 0f, 1f);
            var fadeOut = Math.Clamp((t - 0.6f) / 0.4f, 0f, 1f);
            var strength = wave.Intensity * fadeIn * (1f - fadeOut);

            var warpPx = strength * thicknessPx * 0.5f;

            if (warpPx < 0.01f)
                continue;

            _centers[_count] = centerLocal;
            _radii[_count] = radiusPx;
            _thicknesses[_count] = thicknessPx;
            _warpStrengths[_count] = warpPx;
            _count++;
        }

        if (_count > 0)
            return true;

        foreach (var wave in _system.ActiveWaves)
        {
            if (wave.Flash && wave.MapId == args.MapId)
                return true;
        }

        return false;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null || args.Viewport.Eye == null)
            return;

        var worldHandle = args.WorldHandle;

        if (_count > 0)
        {
            var renderScale = args.Viewport.RenderScale * args.Viewport.Eye.Scale;

            _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
            _shader.SetParameter("renderScale", renderScale);
            _shader.SetParameter("count", _count);
            _shader.SetParameter("center", _centers);
            _shader.SetParameter("radius", _radii);
            _shader.SetParameter("thickness", _thicknesses);
            _shader.SetParameter("warpStrength", _warpStrengths);

            worldHandle.UseShader(_shader);
            worldHandle.DrawRect(args.WorldAABB, Color.White);
            worldHandle.UseShader(null);
        }

        //  nuclear flash overlays drawn ON TOP of all distortion, so the shader doesn't overwrite them
        var now = _timing.CurTime.TotalSeconds;
        foreach (var wave in _system.ActiveWaves)
        {
            if (!wave.Flash)
                continue;

            if (wave.MapId != args.MapId)
                continue;

            var elapsed = (float) Math.Max(0, now - wave.ServerStartSeconds);
            DrawNuclearFlash(worldHandle, args, elapsed, wave.DurationSeconds, wave.FlashColor);
        }
    }

    /// <summary>
    /// Renders a full-screen blinding flash for nuclear detonations.
    /// Timings are stretched to survive frame hitches from explosion.
    /// Phase 1 (0–0.5s): peak flash
    /// Phase 2 (0.5–1.2s): peak to mid
    /// Phase 3 (1.2–3s): glow fade
    /// Phase 4 (3s+): tint fading out
    /// </summary>
    private static void DrawNuclearFlash(
        DrawingHandleWorld handle,
        in OverlayDrawArgs args,
        float elapsed,
        float duration,
        Color flashColor)
    {
        var useClassicWarm = flashColor.R > 0.95f && flashColor.G > 0.95f && flashColor.B > 0.95f;
        var peak = useClassicWarm ? Color.White : Lerp(Color.White, flashColor, 0.35f);
        var mid = useClassicWarm ? new Color(1f, 0.85f, 0.5f) : flashColor;
        var late = useClassicWarm ? new Color(1f, 0.4f, 0.1f) : ScaleRgb(flashColor, 0.55f);
        var end = useClassicWarm ? new Color(1f, 0.3f, 0.05f) : ScaleRgb(flashColor, 0.3f);

        float alpha;
        Color tint;

        if (elapsed < 0.5f)
        {
            alpha = 1f;
            tint = peak;
        }
        else if (elapsed < 1.2f)
        {
            var p = (elapsed - 0.5f) / 0.7f;
            var ease = p * p;
            alpha = 1f - ease * 0.4f;
            tint = Lerp(peak, mid, ease);
        }
        else if (elapsed < 3f)
        {
            var p = (elapsed - 1.2f) / 1.8f;
            var ease = p * p;
            // end at 0.1s, so phase 4 continues the fade instead of popping back up
            alpha = LerpFloat(0.6f, 0.1f, ease);
            tint = Lerp(mid, late, ease);
        }
        else
        {
            var p = Math.Clamp((elapsed - 3f) / Math.Max(duration - 3f, 0.5f), 0f, 1f);
            var ease = p * p;
            alpha = 0.1f * (1f - ease);
            tint = Lerp(late, end, ease);
        }

        if (alpha <= 0.005f)
            return;

        var color = new Color(tint.R, tint.G, tint.B, alpha);
        handle.UseShader(null);
        handle.DrawRect(args.WorldAABB, color);
    }

    private static Color ScaleRgb(Color c, float scale)
    {
        return new Color(c.R * scale, c.G * scale, c.B * scale);
    }

    private static float LerpFloat(float a, float b, float t)
    {
        return a + (b - a) * t;
    }

    private static Color Lerp(Color a, Color b, float t)
    {
        return new Color(
            a.R + (b.R - a.R) * t,
            a.G + (b.G - a.G) * t,
            a.B + (b.B - a.B) * t);
    }
}
