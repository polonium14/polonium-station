// SPDX-FileCopyrightText: 2026 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

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

    private const int MaxRings = 8;
    private const string ShaderID = "Shockwave";
    public ExplosionShockwaveOverlay(ExplosionShockwaveSystem system, IPrototypeManager protoManager, IGameTiming timing)
    {
        _system = system;
        _timing = timing;
        _shader = protoManager.Index<ShaderPrototype>(ShaderID).Instance().Duplicate();
        ZIndex = 102;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return args.Viewport.Eye != null && _system.ActiveWaves.Count > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null || args.Viewport.Eye == null)
            return;

        var viewport = args.Viewport;
        var eye = viewport.Eye!;
        var now = _timing.CurTime.TotalSeconds;
        var worldHandle = args.WorldHandle;
        var renderScale = viewport.RenderScale * eye.Scale;

        var drawn = 0;
        foreach (var wave in _system.ActiveWaves)
        {
            if (drawn >= MaxRings)
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

            var ppm = EyeManager.PixelsPerMeter;
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

            _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
            _shader.SetParameter("center", centerLocal);
            _shader.SetParameter("radius", radiusPx);
            _shader.SetParameter("thickness", thicknessPx);
            _shader.SetParameter("warpStrength", warpPx);
            _shader.SetParameter("renderScale", renderScale);

            worldHandle.UseShader(_shader);
            worldHandle.DrawRect(args.WorldAABB, Color.White);
            worldHandle.UseShader(null);

            drawn++;
        }

        //  nuclear flash overlays drawn ON TOP of all distortion, so the shader doesn't overwrite them
        foreach (var wave in _system.ActiveWaves)
        {
            if (!wave.Flash)
                continue;

            if (wave.MapId != args.MapId)
                continue;

            var elapsed = (float) Math.Max(0, now - wave.ServerStartSeconds);
            DrawNuclearFlash(worldHandle, args, elapsed, wave.DurationSeconds);
        }
    }

    /// <summary>
    /// Renders a full-screen blinding flash for nuclear detonations
    /// Timings are stretched to survive frame hitches from explosion
    /// Phase 1 (0–0.5s): white flash
    /// Phase 2 (0.5–1.2s): white to orange
    /// Phase 3 (1.2–3s): orange glow fade
    /// Phase 4 (3s+): red tint fading out
    /// </summary>
    private static void DrawNuclearFlash(
        DrawingHandleWorld handle,
        in OverlayDrawArgs args,
        float elapsed,
        float duration)
    {
        float alpha;
        Color tint;

        if (elapsed < 0.5f)
        {
            alpha = 1f;
            tint = Color.White;
        }
        else if (elapsed < 1.2f)
        {
            var p = (elapsed - 0.5f) / 0.7f;
            var ease = p * p;
            alpha = 1f - ease * 0.4f;
            tint = Lerp(Color.White, new Color(1f, 0.85f, 0.5f), ease);
        }
        else if (elapsed < 3f)
        {
            var p = (elapsed - 1.2f) / 1.8f;
            var ease = p * p;
            alpha = 0.6f * (1f - ease);
            tint = Lerp(new Color(1f, 0.85f, 0.5f), new Color(1f, 0.4f, 0.1f), ease);
        }
        else
        {
            var p = Math.Clamp((elapsed - 3f) / Math.Max(duration - 3f, 0.5f), 0f, 1f);
            alpha = 0.1f * (1f - p);
            tint = new Color(1f, 0.3f, 0.05f);
        }

        if (alpha <= 0.005f)
            return;

        var color = new Color(tint.R, tint.G, tint.B, alpha);
        handle.UseShader(null);
        handle.DrawRect(args.WorldAABB, color);
    }

    private static Color Lerp(Color a, Color b, float t)
    {
        return new Color(
            a.R + (b.R - a.R) * t,
            a.G + (b.G - a.G) * t,
            a.B + (b.B - a.B) * t);
    }
}
