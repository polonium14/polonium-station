using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Utility;

namespace Content.Client._Polonium.Medical.HealthAnalyzer;

/// <summary>
/// A small triangle pointing the way a reading is moving, or a dash when it holds steady.
/// </summary>
public sealed class TrendMarker : Control
{
    // A reused list rather than a stack span: the client sandbox does not allow stackalloc spans.
    private readonly List<Vector2> _triangle = new(3);

    /// <summary>
    /// 1 rising, -1 falling, 0 steady, null for no trend yet.
    /// </summary>
    public int? Direction { get; set; }

    public Color Color { get; set; } = Color.White;

    public TrendMarker()
    {
        MinSize = new Vector2(10, 10);
        VerticalAlignment = VAlignment.Center;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (Direction is not { } direction)
            return;

        var size = PixelSize;
        if (direction == 0)
        {
            handle.DrawLine(new Vector2(1, size.Y / 2f), new Vector2(size.X - 1, size.Y / 2f), Color);
            return;
        }

        var top = direction > 0 ? 1f : size.Y - 1f;
        var bottom = direction > 0 ? size.Y - 1f : 1f;
        _triangle.Clear();
        _triangle.Add(new Vector2(size.X / 2f, top));
        _triangle.Add(new Vector2(1, bottom));
        _triangle.Add(new Vector2(size.X - 1, bottom));
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _triangle, Color);
    }
}

/// <summary>
/// Lines across the damage chart where critical condition and death start, drawn on top of the chart.
/// </summary>
public sealed class ThresholdMarks : Control
{
    private const float Thickness = 2f;

    public List<(float Fraction, Color Color)> Marks { get; } = new();

    public ThresholdMarks()
    {
        HorizontalExpand = true;
        VerticalExpand = true;
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var size = PixelSize;
        var half = Thickness * UIScale / 2f;
        foreach (var (fraction, color) in Marks)
        {
            var x = Math.Clamp(fraction, 0f, 1f) * size.X;
            handle.DrawRect(new UIBox2(x - half, 0, x + half, size.Y), color);
        }
    }
}

/// <summary>
/// Icons in the row above the damage chart, each centered over its threshold line.
/// </summary>
public sealed class ThresholdIcons : Control
{
    private const float IconSize = 16f;

    /// <summary>
    /// Icons with their place along the chart (0 to 1) and the text shown when hovered.
    /// </summary>
    public List<(float Fraction, Texture Icon, string Tooltip)> Icons { get; } = new();

    public ThresholdIcons()
    {
        HorizontalExpand = true;
        MinSize = new Vector2(0, IconSize);
        MouseFilter = MouseFilterMode.Pass;
        TooltipSupplier = SupplyTooltip;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var size = IconSize * UIScale;
        foreach (var (index, left) in VisibleIcons(PixelSize.X, size))
        {
            handle.DrawTextureRect(Icons[index].Icon, UIBox2.FromDimensions(left, PixelSize.Y - size, size, size));
        }
    }

    /// <summary>
    /// The icons that get drawn and the left edge of each.
    /// From the last one back: when two thresholds sit too close for both icons, the later, graver one stays.
    /// </summary>
    private IEnumerable<(int Index, float Left)> VisibleIcons(float width, float size)
    {
        float? taken = null;
        for (var i = Icons.Count - 1; i >= 0; i--)
        {
            var left = Math.Clamp(Math.Clamp(Icons[i].Fraction, 0f, 1f) * width - size / 2f, 0f, MathF.Max(width - size, 0f));
            if (taken is { } next && left + size > next)
                continue;

            yield return (i, left);
            taken = left;
        }
    }

    private Control? SupplyTooltip(Control sender)
    {
        var mouse = UserInterfaceManager.MousePositionScaled.Position - GlobalPosition;
        foreach (var (index, left) in VisibleIcons(Width, IconSize))
        {
            if (mouse.X < left || mouse.X > left + IconSize)
                continue;

            var tooltip = new Tooltip { Tracking = true };
            tooltip.SetMessage(FormattedMessage.FromUnformatted(Icons[index].Tooltip));
            return tooltip;
        }

        return null;
    }
}
