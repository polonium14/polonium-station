using System.Numerics;
using Content.Client.Resources;
using Content.Shared._Polonium.Breathalyzer;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Breathalyzer;

public sealed partial class BreathalyzerGauge : Control
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IResourceCache _cache = default!;

    private const int Segments = 24;
    private const int SweepTrail = 5;
    private const float SegmentGap = 2f;
    private const float UnlitAlpha = 0.14f;

    private readonly Font _font;
    private BreathalyzerComponent? _comp;
    private BreathalyzerSystem? _breathalyzer;

    public BreathalyzerGauge()
    {
        IoCManager.InjectDependencies(this);
        _font = _cache.GetFont("/Fonts/_Polonium/KodeMono/KodeMono-Regular.ttf", 9);
    }

    public void SetBreathalyzer(BreathalyzerComponent comp, BreathalyzerSystem breathalyzer)
    {
        _comp = comp;
        _breathalyzer = breathalyzer;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (_comp == null || _breathalyzer == null || _comp.ScaleMax <= 0f)
            return;

        var labelHeight = _font.GetLineHeight(UIScale) + 2f * UIScale;
        var tickHeight = 3f * UIScale;
        var barTop = tickHeight + UIScale;
        var barBottom = PixelSize.Y - labelHeight;
        if (barBottom <= barTop)
            return;

        var gap = SegmentGap * UIScale;
        var segmentWidth = (PixelSize.X - gap * (Segments - 1)) / Segments;
        var time = (float) _timing.RealTime.TotalSeconds;

        for (var i = 0; i < Segments; i++)
        {
            var value = (i + 0.5f) / Segments * _comp.ScaleMax;
            var color = _breathalyzer.GetLevel(_comp, value)?.Color ?? BreathalyzerWindow.LcdColor;
            var left = i * (segmentWidth + gap);
            var box = new UIBox2(left, barTop, left + segmentWidth, barBottom);

            handle.DrawRect(box, color.WithAlpha(GetBrightness(i, value, time)));
        }

        foreach (var level in _comp.Levels)
        {
            if (level.Threshold <= 0f || level.Threshold >= _comp.ScaleMax)
                continue;

            var x = level.Threshold / _comp.ScaleMax * PixelSize.X;
            handle.DrawRect(new UIBox2(x - UIScale * 0.5f, 0f, x + UIScale * 0.5f, tickHeight), level.Color);
        }

        for (var mark = 0; mark <= (int) _comp.ScaleMax; mark++)
        {
            var text = mark.ToString();
            var size = handle.GetDimensions(_font, text, UIScale);
            var x = Math.Clamp(mark / _comp.ScaleMax * PixelSize.X - size.X / 2f, 0f, PixelSize.X - size.X);
            handle.DrawString(_font, new Vector2(x, barBottom + 2f * UIScale), text, UIScale, BreathalyzerWindow.LcdDimColor);
        }
    }

    private float GetBrightness(int segment, float value, float time)
    {
        switch (_comp!.State)
        {
            case BreathalyzerState.Blowing:
                return UnlitAlpha + 0.4f * MathF.Max(0f, MathF.Sin(time * 7f - segment * 0.45f));

            case BreathalyzerState.Analyzing:
                var duration = (float) _comp.AnalysisDuration.TotalSeconds;
                var left = (float) (_comp.AnalysisEnd - _timing.CurTime).TotalSeconds;
                var progress = duration > 0f ? Math.Clamp(1f - left / duration, 0f, 1f) : 1f;
                var head = progress * (Segments + SweepTrail);
                var distance = head - segment;
                if (distance < 0f || distance > SweepTrail)
                    return UnlitAlpha;

                return MathF.Max(UnlitAlpha, 1f - distance / SweepTrail);

            case BreathalyzerState.Result when _comp.LastResult is { } result:
                var segmentStart = (float) segment / Segments * _comp.ScaleMax;
                return result > segmentStart ? 1f : UnlitAlpha;

            default:
                return UnlitAlpha;
        }
    }
}
