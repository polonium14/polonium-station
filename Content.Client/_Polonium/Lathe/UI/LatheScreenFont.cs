using System.Text;
using Content.Client._Polonium.UserInterface.PixelUi;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;

namespace Content.Client._Polonium.Lathe.UI;

/// <summary>
/// The typefaces of the console: the game's standard font on its screens, Jersey on paper and on the name plate.
/// Each drawing of Jersey is used only where its own pixels land on whole screen pixels.
/// </summary>
public sealed class LatheScreenFont
{
    private const string TextPath = "/Fonts/NotoSans/NotoSans-Regular.ttf";
    private const string CoarsePath = "/Fonts/_Polonium/Jersey10/Jersey10-Regular.ttf";
    private const string FinePath = "/Fonts/_Polonium/Jersey15/Jersey15-Regular.ttf";
    private const int TextSize = 10;
    private const int ReadoutSize = 21;
    private const int FineLine = 27;
    private const int CoarseLine = 20;

    private readonly Font _text;
    private readonly Font _readout;

    /// <summary>
    /// Jersey 10 is drawn on a 56/3 pixel em. The engine renders 4/3 pixels per point, so that is size 14.
    /// </summary>
    public readonly PixelFont Coarse;

    /// <summary>
    /// Jersey 15 is drawn on a 27 pixel em, which is size 27 at three quarters of the engine's scale.
    /// </summary>
    public readonly PixelFont Fine;

    public LatheScreenFont(IResourceCache cache)
    {
        _text = cache.GetFont(TextPath, TextSize);
        _readout = cache.GetFont(TextPath, ReadoutSize);
        Coarse = new PixelFont(cache.GetFont(CoarsePath, 14), 1f, 5, 10);
        Fine = new PixelFont(cache.GetFont(FinePath, 27), 0.75f, 6, 15);
    }

    public LatheScreenFace Face(int scale)
    {
        return Standard(_text, scale);
    }

    public LatheScreenFace Readout(int scale)
    {
        return Standard(_readout, scale);
    }

    public LatheScreenFace PaperFace(int scale)
    {
        if (scale % 2 != 0 && scale % 3 == 0)
            return new LatheScreenFace(Fine, scale / 3, FineLine);

        return new LatheScreenFace(Coarse, Math.Max(1, scale / 2), CoarseLine);
    }

    private static LatheScreenFace Standard(Font font, int scale)
    {
        var size = scale / 2f;
        var capital = font.GetCharMetrics(new Rune('H'), size);
        var capHeight = capital?.Height ?? 0;
        var capTop = font.GetAscent(size) - (capital?.BearingY ?? 0);
        return new LatheScreenFace(new PixelFont(font, size, capTop, capHeight), 1, font.GetHeight(size));
    }
}

/// <param name="Dot">Screen pixels per pixel of the font.</param>
/// <param name="Line">Height of a text line in font pixels.</param>
public readonly record struct LatheScreenFace(PixelFont Font, int Dot, int Line)
{
    public float DrawScale => Font.Scale * Dot;

    /// <summary>
    /// Width in screen pixels, without the empty column after the last glyph.
    /// </summary>
    public int Width(ReadOnlySpan<char> text)
    {
        return text.IsEmpty ? 0 : (Font.Measure(text) - 1) * Dot;
    }

    public int Fit(ReadOnlySpan<char> text, int pixels)
    {
        return Font.Fit(text, pixels / Dot);
    }
}
