using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

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
