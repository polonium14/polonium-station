using Content.Client._Polonium.UserInterface.PixelUi;
using Content.Client.Resources;
using Robust.Client.ResourceManagement;

namespace Content.Client._Polonium.Lathe.UI;

/// <summary>
/// The typeface of the green screen. A capital is five art pixels tall, and the screen draws it with finer dots
/// than the body: ten rows on even scales, fifteen on scales that divide by three. Each drawing of Jersey is
/// used only where its own pixels land on whole screen pixels.
/// </summary>
public sealed class LatheScreenFont
{
    private const string CoarsePath = "/Fonts/_Polonium/Jersey10/Jersey10-Regular.ttf";
    private const string FinePath = "/Fonts/_Polonium/Jersey15/Jersey15-Regular.ttf";
    private const int FineLine = 27;

    /// <summary>
    /// Height of a text line of the coarse font in its own pixels.
    /// </summary>
    public const int CoarseLine = 20;

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
        Coarse = new PixelFont(cache.GetFont(CoarsePath, 14), 1f, 5, 10);
        Fine = new PixelFont(cache.GetFont(FinePath, 27), 0.75f, 6, 15);
    }

    public LatheScreenFace Face(int scale)
    {
        if (scale % 2 != 0 && scale % 3 == 0)
            return new LatheScreenFace(Fine, scale / 3, FineLine);

        return new LatheScreenFace(Coarse, Math.Max(1, scale / 2), CoarseLine);
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
