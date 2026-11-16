using System.Numerics;
using Content.Client.Resources;
using Content.Shared.CCVar;
using Robust.Client.ResourceManagement;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Medical.HealthAnalyzer;

/// <summary>
/// A thin horizontal bar for the health analyzer: a fill or a pointer that glides to new readings, threshold lines, and
/// a shaded safe band. A dead organ draws a hatched track and a missing one only an outline, so the state never rests
/// on color alone.
/// </summary>
public sealed partial class AnalyzerBar : Control
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IResourceCache _resources = default!;

    private static readonly Color TextShadow = Color.Black.WithAlpha(0.8f);
    private static readonly Color Track = Color.FromHex("#1b1b1e");
    private static readonly Color Line = Color.FromHex("#d0d0d0");
    private static readonly Color Band = Color.FromHex("#3c854a").WithAlpha(0.35f);
    private static readonly Color Hatch = Color.FromHex("#757575");

    private const float TweenInverseHalfLife = 8f;

    private float _shown;

    /// <summary>
    /// Where the reading sits, 0 to 1.
    /// </summary>
    public float Target { get; set; }

    public Color Color { get; set; } = Color.White;

    /// <summary>
    /// Draws a pointer at the reading instead of a fill, for scales where both ends are bad (temperature).
    /// </summary>
    public bool Pointer { get; set; }

    public BarMode Mode { get; set; } = BarMode.Fill;

    /// <summary>
    /// Threshold lines, 0 to 1.
    /// </summary>
    public List<float> Notches { get; } = new();

    /// <summary>
    /// A shaded range that is fine to be in, 0 to 1.
    /// </summary>
    public (float From, float To)? SafeBand { get; set; }

    /// <summary>
    /// Text drawn inside the bar on the left, such as the organ's name.
    /// </summary>
    public string? LeftText { get; set; }

    /// <summary>
    /// Text drawn inside the bar on the right, such as the reading.
    /// </summary>
    public string? RightText { get; set; }

    private Font? _font;

    public AnalyzerBar()
    {
        IoCManager.InjectDependencies(this);
        MinHeight = 6;
        HorizontalExpand = true;
    }

    /// <summary>
    /// Jumps straight to the reading, for a new patient.
    /// </summary>
    public void Snap()
    {
        _shown = Target;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_shown == Target)
            return;

        if (_cfg.GetCVar(CCVars.ReducedMotion))
        {
            _shown = Target;
            return;
        }

        _shown = MathHelper.Lerp(_shown, Target, MathHelper.Clamp01(TweenInverseHalfLife * args.DeltaSeconds));
        if (MathF.Abs(_shown - Target) < 0.001f)
            _shown = Target;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var size = PixelSize;
        var box = new UIBox2(0, 0, size.X, size.Y);

        if (Mode == BarMode.Outline)
        {
            handle.DrawRect(box, Hatch, filled: false);
            DrawTexts(handle, size);
            return;
        }

        handle.DrawRect(box, Track);

        if (Mode == BarMode.Hatched)
        {
            // Diagonal hatching over the whole track: the organ is dead.
            // Each line rises one pixel per pixel; cut it at both ends so it stays inside the bar.
            for (var x = -size.Y; x < size.X; x += 6)
            {
                var from = Math.Max(x, 0);
                var to = Math.Min(x + size.Y, size.X);
                handle.DrawLine(new Vector2(from, size.Y - (from - x)), new Vector2(to, size.Y - (to - x)), Hatch);
            }

            DrawTexts(handle, size);
            return;
        }

        if (SafeBand is { } band)
            handle.DrawRect(new UIBox2(band.From * size.X, 0, band.To * size.X, size.Y), Band);

        var at = Math.Clamp(_shown, 0f, 1f) * size.X;
        if (Pointer)
            handle.DrawRect(new UIBox2(at - 2, 0, at + 2, size.Y), Color);
        else
            handle.DrawRect(new UIBox2(0, 0, at, size.Y), Color);

        foreach (var notch in Notches)
        {
            var x = Math.Clamp(notch, 0f, 1f) * size.X;
            handle.DrawLine(new Vector2(x, 0), new Vector2(x, size.Y), Line);
        }

        DrawTexts(handle, size);
    }

    /// <summary>
    /// The texts inside the bar, left and right, vertically centered and white with a dark shadow so they read on any
    /// fill.
    /// </summary>
    private void DrawTexts(DrawingHandleScreen handle, Vector2i size)
    {
        if (LeftText == null && RightText == null)
            return;

        _font ??= _resources.GetFont("/Fonts/NotoSans/NotoSans-Regular.ttf", 11);
        var padding = 4f * UIScale;
        var y = (size.Y - _font.GetLineHeight(UIScale)) / 2f;

        if (LeftText is { } left)
            DrawShadowed(handle, new Vector2(padding, y), left);

        if (RightText is { } right)
        {
            var width = handle.GetDimensions(_font, right, UIScale).X;
            DrawShadowed(handle, new Vector2(size.X - padding - width, y), right);
        }
    }

    private void DrawShadowed(DrawingHandleScreen handle, Vector2 at, string text)
    {
        handle.DrawString(_font!, at + new Vector2(1, 1), text, UIScale, TextShadow);
        handle.DrawString(_font!, at, text, UIScale, Color.White);
    }
}

public enum BarMode : byte
{
    Fill,

    /// <summary>
    /// Dead: hatched track, no reading.
    /// </summary>
    Hatched,

    /// <summary>
    /// Missing or no reading: an empty outline.
    /// </summary>
    Outline,
}
