using Content.Client._Polonium.UserInterface.PixelUi;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._Polonium.Lathe.UI;

/// <summary>
/// The plotter pane: the item being printed appears pixel by pixel behind a moving head.
/// </summary>
public sealed class LathePlot : Control
{
    public const int ArtWidth = 116;
    public const int ArtHeight = 84;

    private const int GridStep = 8;
    private const int GridOffset = 4;
    private const int MaxItemWidth = 100;
    private const int MaxItemHeight = 72;
    private const int MaxItemScale = 5;
    private const int TrailLength = 12;
    private const float TrailAlpha = 0.55f;
    private const float SlideTime = 0.22f;
    private const int TearShiftMin = 4;
    private const int TearShiftSpread = 4;
    private const int TearShiftSalt = 2;

    private static readonly Color Crosshair = LathePalette.Screen.WithAlpha(0.4f);

    private readonly LatheConsoleModel _model;
    private readonly LatheIconCache _icons;

    private DrawingHandleScreen _handle = default!;
    private int _scale;
    private int _tearTop;
    private int _tearShift;

    /// <summary>
    /// Where the head stands, in pixels of the item.
    /// </summary>
    public Vector2i Head;

    public LathePlot(LatheConsoleModel model, LatheIconCache icons)
    {
        _model = model;
        _icons = icons;
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        _handle = handle;
        _scale = PixelCanvas.GetScale(UIScale);
        _tearTop = LatheScreenOverlay.SliceTop(_model) - LatheScreen.PlotTop;
        _tearShift = TearShiftMin + _model.Glitch(TearShiftSalt, TearShiftSpread);
        Head = Vector2i.Zero;

        for (var x = GridOffset; x < ArtWidth; x += GridStep)
        {
            Fill(x, 0, 1, ArtHeight, LathePalette.ScreenGrid);
        }

        for (var y = GridOffset; y < ArtHeight; y += GridStep)
        {
            Fill(0, y, ArtWidth, 1, LathePalette.ScreenGrid);
        }

        var shown = _model.Printing ?? _model.Hover;
        if (shown != null && _icons.Get(shown, true) is { Pixels: { Length: > 0 } pixels } icon)
            DrawItem(icon, pixels, _model.Printing != null);

        if (!_model.ReducedMotion)
            DrawLeaving();

    }

    private static int ItemScale(LatheIcon icon)
    {
        return Math.Clamp(Math.Min(MaxItemWidth / icon.Bounds.Width, MaxItemHeight / icon.Bounds.Height), 1, MaxItemScale);
    }

    private void DrawItem(LatheIcon icon, LatheIconPixel[] pixels, bool printing)
    {
        var bounds = icon.Bounds;
        var scale = ItemScale(icon);
        var left = (ArtWidth - bounds.Width * scale) / 2;
        var top = (ArtHeight - bounds.Height * scale) / 2;
        var done = printing ? (int) (_model.Progress * pixels.Length) : 0;
        var half = scale / 2;
        var dotted = scale > 2;
        var pending = printing ? LathePalette.ScreenLow : LathePalette.ScreenMid;

        for (var i = done; i < pixels.Length; i++)
        {
            var x = left + (pixels[i].X - bounds.Left) * scale;
            var y = top + (pixels[i].Y - bounds.Top) * scale;
            if (dotted)
                Fill(x + half, y + half, 1, 1, pending);
            else
                Fill(x, y, scale, scale, pending);
        }

        for (var i = 0; i < done; i++)
        {
            Fill(left + (pixels[i].X - bounds.Left) * scale, top + (pixels[i].Y - bounds.Top) * scale, scale, scale, pixels[i].Color);
        }

        if (!printing)
            return;

        if (!_model.ReducedMotion)
        {
            var from = Math.Max(0, done - TrailLength);
            for (var i = from; i < done; i++)
            {
                var alpha = TrailAlpha * (i - (done - TrailLength)) / TrailLength;
                Fill(left + (pixels[i].X - bounds.Left) * scale, top + (pixels[i].Y - bounds.Top) * scale, scale, scale, LathePalette.ScreenHot.WithAlpha(alpha));
            }
        }

        var head = pixels[Math.Min(done, pixels.Length - 1)];
        Head = new Vector2i(head.X - bounds.Left, head.Y - bounds.Top);
        var headX = left + Head.X * scale;
        var headY = top + Head.Y * scale;
        Fill(0, headY + half, ArtWidth, 1, Crosshair);
        Fill(headX + half, 0, 1, ArtHeight, Crosshair);
        Fill(headX - 1, headY - 1, scale + 2, 1, LathePalette.ScreenHot);
        Fill(headX - 1, headY + scale, scale + 2, 1, LathePalette.ScreenHot);
        Fill(headX - 1, headY, 1, scale, LathePalette.ScreenHot);
        Fill(headX + scale, headY, 1, scale, LathePalette.ScreenHot);
    }

    /// <summary>
    /// A finished item slides off the plot to the right, towards the output tray.
    /// </summary>
    private void DrawLeaving()
    {
        foreach (var output in _model.Outputs)
        {
            var elapsed = (float) (_model.Now - output.Time).TotalSeconds;
            if (elapsed >= SlideTime || _icons.Get(output.Recipe, true) is not { Pixels: { Length: > 0 } pixels } icon)
                continue;

            var bounds = icon.Bounds;
            var scale = ItemScale(icon);
            var left = (ArtWidth - bounds.Width * scale) / 2;
            var top = (ArtHeight - bounds.Height * scale) / 2;
            left += (int) MathF.Round(elapsed / SlideTime * (ArtWidth - left));
            foreach (var pixel in pixels)
            {
                Fill(left + (pixel.X - bounds.Left) * scale, top + (pixel.Y - bounds.Top) * scale, scale, scale, pixel.Color);
            }
        }
    }

    /// <summary>
    /// On an emagged lathe one slice of the plot sits a few pixels to the side.
    /// </summary>
    private void Fill(int x, int y, int width, int height, Color color)
    {
        var sliceBottom = _tearTop + LatheScreenOverlay.SliceHeight;
        if (!_model.Emagged || y >= sliceBottom || y + height <= _tearTop)
        {
            Block(x, y, width, height, color);
            return;
        }

        var bandTop = Math.Max(y, _tearTop);
        var bandBottom = Math.Min(y + height, sliceBottom);
        Block(x, y, width, bandTop - y, color);
        Block(x + _tearShift, bandTop, width, bandBottom - bandTop, color);
        Block(x, bandBottom, width, y + height - bandBottom, color);
    }

    private void Block(int x, int y, int width, int height, Color color)
    {
        var right = Math.Min(x + width, ArtWidth);
        var bottom = Math.Min(y + height, ArtHeight);
        x = Math.Max(x, 0);
        y = Math.Max(y, 0);
        if (right > x && bottom > y)
            _handle.DrawRect(UIBox2.FromDimensions(x * _scale, y * _scale, (right - x) * _scale, (bottom - y) * _scale), color);
    }
}
