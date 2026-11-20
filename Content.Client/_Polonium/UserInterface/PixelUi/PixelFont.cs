using System.Text;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;

namespace Content.Client._Polonium.UserInterface.PixelUi;

/// <summary>
/// A pixel font together with the size of its own pixels, measured in art pixels.
/// </summary>
/// <param name="Scale">How many art pixels one pixel of the font covers.</param>
/// <param name="CapTop">Rows between the top of a text line and the top of a capital letter.</param>
/// <param name="CapHeight">Height of a capital letter in font pixels.</param>
public readonly record struct PixelFont(Font Font, float Scale, int CapTop, int CapHeight)
{
    private const string Tiny5Path = "/Fonts/_Polonium/Tiny5/Tiny5-Regular.ttf";

    /// <summary>
    /// Tiny5 is drawn on an 8 pixel em. The engine renders 4/3 pixels per point, so that is size 6.
    /// </summary>
    public static PixelFont Tiny5(IResourceCache cache)
    {
        return new PixelFont(cache.GetFont(Tiny5Path, 6), 1f, 2, 5);
    }

    /// <summary>
    /// Width of the text in art pixels, including the empty column after the last glyph.
    /// </summary>
    public int Measure(ReadOnlySpan<char> text)
    {
        var width = 0;
        while (!text.IsEmpty)
        {
            text = text[Decode(text, out var rune)..];
            width += Font.GetCharMetrics(rune, Scale)?.Advance ?? 0;
        }

        return width;
    }

    /// <summary>
    /// How many characters from the start of the text fit into the given width in art pixels.
    /// </summary>
    public int Fit(ReadOnlySpan<char> text, int width)
    {
        var used = 0;
        var count = 0;
        while (count < text.Length)
        {
            var length = Decode(text[count..], out var rune);
            var advance = Font.GetCharMetrics(rune, Scale)?.Advance ?? 0;
            if (used + advance - 1 > width)
                break;

            used += advance;
            count += length;
        }

        return count;
    }

    private static int Decode(ReadOnlySpan<char> text, out Rune rune)
    {
        if (Rune.TryCreate(text[0], out rune))
            return 1;

        if (text.Length > 1 && Rune.TryCreate(text[0], text[1], out rune))
            return 2;

        rune = Rune.ReplacementChar;
        return 1;
    }
}
