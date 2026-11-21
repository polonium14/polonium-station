using System.Numerics;
using Content.Client._Polonium.UserInterface.PixelUi;
using Robust.Client.Graphics;

namespace Content.Client._Polonium.Lathe.UI;

/// <summary>
/// Draws on the green screen. Positions are art pixels like everywhere on the console, but the screen
/// has finer dots than the body: hairlines are half an art pixel where the scale allows it, text follows the font.
/// </summary>
public readonly struct LatheScreenCanvas
{
    private readonly DrawingHandleScreen _handle;
    private readonly LatheScreenFace _face;

    public readonly int Scale;
    public readonly int Dot;

    public LatheScreenCanvas(DrawingHandleScreen handle, LatheScreenFont font, float uiScale)
    {
        _handle = handle;
        Scale = PixelCanvas.GetScale(uiScale);
        Dot = GetDot(Scale);
        _face = font.Face(Scale);
    }

    public static int GetDot(int scale)
    {
        return Math.Max(1, scale / 2);
    }

    public void Rect(int x, int y, int width, int height, Color color)
    {
        if (width > 0 && height > 0)
            _handle.DrawRect(UIBox2.FromDimensions(x * Scale, y * Scale, width * Scale, height * Scale), color);
    }

    public void Texture(Texture texture, UIBox2 source, int x, int y, Color? modulate = null)
    {
        _handle.DrawTextureRectRegion(texture, UIBox2.FromDimensions(x * Scale, y * Scale, source.Width * Scale, source.Height * Scale), source, modulate);
    }

    /// <summary>
    /// A hairline whose bottom edge lies on the given row.
    /// </summary>
    public void RuleAbove(int x, int y, int width, Color color)
    {
        _handle.DrawRect(UIBox2.FromDimensions(x * Scale, y * Scale - Dot, width * Scale, Dot), color);
    }

    public void RuleBelow(int x, int y, int width, Color color)
    {
        _handle.DrawRect(UIBox2.FromDimensions(x * Scale, y * Scale, width * Scale, Dot), color);
    }

    /// <summary>
    /// A hairline whose right edge lies on the given column.
    /// </summary>
    public void RuleLeftOf(int x, int y, int height, Color color)
    {
        _handle.DrawRect(UIBox2.FromDimensions(x * Scale - Dot, y * Scale, Dot, height * Scale), color);
    }

    /// <summary>
    /// Width of the text in screen pixels.
    /// </summary>
    public int Width(ReadOnlySpan<char> text)
    {
        return _face.Width(text);
    }

    public int Fit(ReadOnlySpan<char> text, int width)
    {
        return _face.Fit(text, width * Scale);
    }

    /// <summary>
    /// Draws a line of text centred vertically in a band, cut off at the given width.
    /// </summary>
    public void Text(int x, int y, int height, int width, ReadOnlySpan<char> text, Color color)
    {
        DrawAt(x * Scale, y, height, text[..Fit(text, width)], color);
    }

    public void TextRight(int right, int y, int height, ReadOnlySpan<char> text, Color color)
    {
        DrawAt(right * Scale - Width(text), y, height, text, color);
    }

    public void TextCentered(int x, int y, int height, int width, ReadOnlySpan<char> text, Color color)
    {
        var shown = text[..Fit(text, width)];
        DrawAt(x * Scale + (width * Scale - Width(shown)) / 2, y, height, shown, color);
    }

    private void DrawAt(int pixelX, int y, int height, ReadOnlySpan<char> text, Color color)
    {
        var top = y * Scale + (height * Scale - _face.Line * _face.Dot) / 2;
        _handle.DrawString(_face.Font.Font, new Vector2(pixelX, top), text, _face.DrawScale, color);
    }
}
