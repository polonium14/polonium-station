using System.Numerics;
using Content.Client._Polonium.UserInterface.PixelUi;
using Robust.Client.GameObjects;

namespace Content.Client._Polonium.Disposal.UI;

/// <summary>
/// Colours and small drawing helpers shared by the disposal and mailing unit windows.
/// </summary>
public static class DisposalDeviceArt
{
    public const string TexturePath = "/Textures/_Polonium/Interface/Disposal/";

    public static readonly Color Mouth = Color.FromHex("#242329");
    public static readonly Color Steel = Color.FromHex("#848893");
    public static readonly Color SteelHigh = Color.FromHex("#b8bec6");
    public static readonly Color SteelEdge = Color.FromHex("#585868");
    public static readonly Color Red = Color.FromHex("#d74722");
    public static readonly Color RedMid = Color.FromHex("#a62228");
    public static readonly Color RedLow = Color.FromHex("#6b2329");
    public static readonly Color RedHigh = Color.FromHex("#f0714a");
    public static readonly Color MailFace = Color.FromHex("#1769ac");
    public static readonly Color MailHigh = Color.FromHex("#3f8fd0");
    public static readonly Color MailMid = Color.FromHex("#0d508d");
    public static readonly Color MailEdge = Color.FromHex("#002b60");
    public static readonly Color Lever = Color.FromHex("#b90c00");
    public static readonly Color LeverHigh = Color.FromHex("#d90e00");
    public static readonly Color LeverLow = Color.FromHex("#971006");
    public static readonly Color Paper = Color.FromHex("#e9e3cf");
    public static readonly Color PaperLow = Color.FromHex("#bdb59c");
    public static readonly Color PaperFaint = Color.FromHex("#8c8674");
    public static readonly Color Ink = Color.FromHex("#1b1e26");
    public static readonly Color Board = Color.FromHex("#0a0d18");
    public static readonly Color Flap = Color.FromHex("#232a3b");
    public static readonly Color FlapHigh = Color.FromHex("#2f3850");
    public static readonly Color FlapText = Color.FromHex("#cfd6e6");
    public static readonly Color KeyFace = Color.FromHex("#c9ced6");
    public static readonly Color KeyHigh = Color.FromHex("#eef1f5");
    public static readonly Color KeyLow = Color.FromHex("#8b919c");
    public static readonly Color KeyInk = Color.FromHex("#17181c");
    public static readonly Color KeyDark = Color.FromHex("#3a3c45");
    public static readonly Color KeyDarkHigh = Color.FromHex("#5a5d69");
    public static readonly Color KeyDarkLow = Color.FromHex("#222329");
    public static readonly Color RockerDark = Color.FromHex("#4a4c57");
    public static readonly Color RockerLight = Color.FromHex("#8d919b");
    public static readonly Color Cross = Color.FromHex("#e8e8ea");
    public static readonly Color PowerMark = Color.FromHex("#aeff37");

    /// <summary>
    /// Smooth travel of a handle from 0 to 1.
    /// </summary>
    public static float Ease(float t)
    {
        return t * t * (3f - 2f * t);
    }

    /// <summary>
    /// The inverse of <see cref="Ease"/>.
    /// </summary>
    public static float Unease(float y)
    {
        return 0.5f - MathF.Sin(MathF.Asin(1f - 2f * y) / 3f);
    }

    /// <summary>
    /// Rounds a value between 0 and 1 to one of a few even steps, so that a change is never a flicker.
    /// </summary>
    public static float Steps(float value, int count)
    {
        return MathF.Round(Math.Clamp(value, 0f, 1f) * count) / count;
    }

    /// <summary>
    /// How high a parabola from <paramref name="start"/> to <paramref name="end"/> must bow so that its top reaches <paramref name="apex"/>.
    /// </summary>
    public static float ArcHeight(float start, float apex, float end)
    {
        var span = end - start;
        var lift = span + 2f * (start - apex);
        return (lift + MathF.Sqrt(lift * lift - span * span)) / 4f;
    }
}

/// <summary>
/// Draws in the coordinates of a unit's body, wherever the body stands in its window.
/// </summary>
public readonly struct DisposalDeviceBrush(PixelCanvas canvas, PixelFont font, int offsetX, int offsetY)
{
    public void Rect(int x, int y, int width, int height, Color color)
    {
        canvas.Rect(x + offsetX, y + offsetY, width, height, color);
    }

    /// <summary>
    /// Writes a line of text with the top of its capitals at the given height and returns its width.
    /// </summary>
    public int Text(string text, int x, int y, Color color)
    {
        canvas.Text(font, x + offsetX, y + offsetY - font.CapTop, text, color);
        return font.Measure(text);
    }

    public void Entity(EntityUid uid, SpriteComponent sprite, int centerX, int centerY, int scale, Direction direction = Direction.South)
    {
        canvas.Entity(uid, sprite, centerX + offsetX, centerY + offsetY, new Vector2(scale), direction);
    }

    /// <summary>
    /// A soft halo around a lit lamp: three rings fading outwards, the outer ones without corners.
    /// </summary>
    public void Glow(int x, int y, int size, Color color, float alpha)
    {
        var outer = color.WithAlpha(alpha * 0.3f);
        Rect(x - 3, y + 1, size + 6, size - 2, outer);
        Rect(x + 1, y - 3, size - 2, size + 6, outer);
        var middle = color.WithAlpha(alpha * 0.5f);
        Rect(x - 2, y, size + 4, size, middle);
        Rect(x, y - 2, size, size + 4, middle);
        Rect(x - 1, y - 1, size + 2, size + 2, color.WithAlpha(alpha));
    }

    public void Arrow(int centerX, int y, bool up, Color color, int rows)
    {
        for (var i = 0; i < rows; i++)
        {
            var half = up ? i : rows - 1 - i;
            Rect(centerX - half, y + i, 1 + 2 * half, 1, color);
        }
    }

    /// <summary>
    /// A square key seen from the front. Its face sinks by one pixel while it is held and the method returns that depth.
    /// </summary>
    public int Key(int x, int y, int width, int height, Color face, Color high, Color low, bool down)
    {
        var depth = down ? 1 : 0;
        Rect(x, y, width, height, DisposalDeviceArt.KeyInk);
        Rect(x + 1, y + 1 + depth, width - 2, height - 3, face);
        Rect(x + 1, y + 1 + depth, width - 2, 1, high);
        if (!down)
            Rect(x + 1, y + height - 2, width - 2, 1, low);

        return depth;
    }
}
