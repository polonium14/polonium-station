using System.Numerics;
using Content.Shared._Polonium.Crosshairs;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.Utility;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client._Polonium.Crosshairs;

// The still middle of the crosshair is a system cursor. The OS draws it without the frame delay of the overlay,
// so the aim point follows the mouse exactly and only the parts showing spread trail behind.
public sealed partial class CombatCrosshairOverlay
{
    private const int CursorKinds = 4;

    private readonly ICursor?[] _cursors = new ICursor?[CursorKinds];
    private float _cursorScale = float.NaN;
    private Control? _cursorViewport;

    private enum CursorKind : byte
    {
        Blank,
        Dot,
        PixelCell,
        Chevrons,
    }

    private static CursorKind CursorKindOf(CrosshairShape shape)
    {
        return shape switch
        {
            CrosshairShape.Grid => CursorKind.Blank,
            CrosshairShape.PixelCross => CursorKind.PixelCell,
            CrosshairShape.Chevrons => CursorKind.Chevrons,
            _ => CursorKind.Dot,
        };
    }

    private void InvalidateCursors()
    {
        _cursorScale = float.NaN;
    }

    private void SetCursor(Control viewport, CursorKind kind, float scale)
    {
        if (_cursorViewport != viewport)
            RestoreCursor();

        ICursor?[]? stale = null;
        if (_cursorScale != scale)
        {
            stale = (ICursor?[])_cursors.Clone();
            Array.Clear(_cursors);
            _cursorScale = scale;
        }

        var cursor = _cursors[(int)kind] ??= BuildCursor(kind, scale);
        if (viewport.CustomCursorShape != cursor)
            viewport.CustomCursorShape = cursor;

        _cursorViewport = viewport;

        if (stale == null)
            return;

        foreach (var old in stale)
        {
            old?.Dispose();
        }
    }

    private void RestoreCursor()
    {
        if (_cursorViewport == null)
            return;

        _cursorViewport.CustomCursorShape = null;
        _cursorViewport = null;
    }

    private void DisposeCursors()
    {
        RestoreCursor();
        foreach (var cursor in _cursors)
        {
            cursor?.Dispose();
        }

        Array.Clear(_cursors);
    }

    private ICursor BuildCursor(CursorKind kind, float scale)
    {
        if (kind == CursorKind.Blank)
        {
            using var blank = new SixLabors.ImageSharp.Image<Rgba32>(32, 32);
            return _clyde.CreateCursor(blank, Vector2i.Zero);
        }

        var fill = new List<Vector2>();
        var outline = new List<Vector2>();
        AddCursorShape(fill, kind, scale, 0f);
        if (_outline)
            AddCursorShape(outline, kind, scale, 1f);

        var min = new Vector2(float.MaxValue);
        var max = new Vector2(float.MinValue);
        foreach (var vertex in fill)
        {
            min = Vector2.Min(min, vertex);
            max = Vector2.Max(max, vertex);
        }

        foreach (var vertex in outline)
        {
            min = Vector2.Min(min, vertex);
            max = Vector2.Max(max, vertex);
        }

        var left = (int)MathF.Floor(min.X);
        var top = (int)MathF.Floor(min.Y);
        var width = (int)MathF.Ceiling(max.X) - left;
        var height = (int)MathF.Ceiling(max.Y) - top;
        using var image = new SixLabors.ImageSharp.Image<Rgba32>(width, height);

        // Pixel centres are sampled like the GPU does, so the cursor matches what the overlay used to draw.
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var point = new Vector2(left + x + 0.5f, top + y + 0.5f);
                var pixel = Covers(outline, point) ? OutlineColor : Color.Transparent;
                if (Covers(fill, point))
                    pixel = Over(_color, pixel);

                image[x, y] = pixel.ConvertImgSharp();
            }
        }

        return _clyde.CreateCursor(image, new Vector2i(-left, -top));
    }

    private static void AddCursorShape(List<Vector2> triangles, CursorKind kind, float scale, float grow)
    {
        switch (kind)
        {
            case CursorKind.Dot:
                var half = new Vector2(BaseThickness * scale * 0.45f + grow);
                AddRect(triangles, -half, half);
                break;
            case CursorKind.PixelCell:
                var size = PixelCellSize(scale);
                var corner = new Vector2(-MathF.Floor(size / 2));
                if (grow == 0f)
                {
                    AddRect(triangles, corner, corner + new Vector2(size));
                    break;
                }

                foreach (var offset in PixelNeighbours)
                {
                    var cell = corner + (Vector2)offset * size;
                    AddRect(triangles, cell, cell + new Vector2(size));
                }

                break;
            case CursorKind.Chevrons:
                AddChevrons(triangles, scale, grow);
                break;
        }
    }

    private static void AddChevrons(List<Vector2> triangles, float scale, float grow)
    {
        var apex = ChevronApex * scale;
        var end = ChevronEnd * scale;
        var halfWidth = ChevronHalfWidth * scale;
        var width = ChevronWidth * scale;

        var slope = halfWidth / (apex - end);
        var sin = halfWidth / MathF.Sqrt(halfWidth * halfWidth + (apex - end) * (apex - end));
        var outerApex = apex + grow / sin;
        var innerApex = apex - (width + grow) / sin;
        var cut = end - grow;
        var outerSide = slope * (outerApex - cut);
        var innerSide = MathF.Max(0f, slope * (innerApex - cut));

        foreach (var dir in Axes)
        {
            var side = new Vector2(-dir.Y, dir.X);
            var outerLeft = dir * cut - side * outerSide;
            var innerLeft = dir * cut - side * innerSide;
            var outerTip = dir * outerApex;
            var innerTip = dir * innerApex;
            var outerRight = dir * cut + side * outerSide;
            var innerRight = dir * cut + side * innerSide;
            AddTriangle(triangles, outerLeft, innerLeft, outerTip);
            AddTriangle(triangles, innerLeft, outerTip, innerTip);
            AddTriangle(triangles, outerTip, innerTip, outerRight);
            AddTriangle(triangles, innerTip, outerRight, innerRight);
        }
    }

    private static void AddRect(List<Vector2> triangles, Vector2 min, Vector2 max)
    {
        AddTriangle(triangles, min, new Vector2(max.X, min.Y), max);
        AddTriangle(triangles, min, max, new Vector2(min.X, max.Y));
    }

    private static void AddTriangle(List<Vector2> triangles, Vector2 a, Vector2 b, Vector2 c)
    {
        triangles.Add(a);
        triangles.Add(b);
        triangles.Add(c);
    }

    private static bool Covers(List<Vector2> triangles, Vector2 point)
    {
        for (var i = 0; i < triangles.Count; i += 3)
        {
            var ab = Cross(triangles[i], triangles[i + 1], point);
            var bc = Cross(triangles[i + 1], triangles[i + 2], point);
            var ca = Cross(triangles[i + 2], triangles[i], point);
            if (ab >= 0 && bc >= 0 && ca >= 0 || ab <= 0 && bc <= 0 && ca <= 0)
                return true;
        }

        return false;
    }

    private static float Cross(Vector2 a, Vector2 b, Vector2 point)
    {
        return (b.X - a.X) * (point.Y - a.Y) - (b.Y - a.Y) * (point.X - a.X);
    }

    private static Color Over(Color top, Color bottom)
    {
        var below = bottom.A * (1 - top.A);
        var alpha = top.A + below;
        if (alpha <= 0f)
            return Color.Transparent;

        return new Color(
            (top.R * top.A + bottom.R * below) / alpha,
            (top.G * top.A + bottom.G * below) / alpha,
            (top.B * top.A + bottom.B * below) / alpha,
            alpha);
    }
}
