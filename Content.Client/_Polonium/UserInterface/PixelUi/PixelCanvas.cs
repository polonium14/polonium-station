using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client._Polonium.UserInterface.PixelUi;

/// <summary>
/// Draws pixel art inside a control. Coordinates are art pixels: one texel of a texture,
/// shown as a whole number of screen pixels so that nothing is ever blurred or uneven.
/// </summary>
public readonly struct PixelCanvas
{
    private const int DigitWidth = 8;
    private const int DigitHeight = 14;
    private const int DigitAdvance = 11;
    private const int ColonAdvance = 5;
    private const int GhostDigit = 10;
    private const int ColonCell = 11;

    private readonly DrawingHandleScreen _handle;

    public readonly int Scale;

    public PixelCanvas(DrawingHandleScreen handle, float uiScale)
    {
        _handle = handle;
        Scale = GetScale(uiScale);
    }

    /// <summary>
    /// How many screen pixels one art pixel covers at the given interface scale.
    /// </summary>
    public static int GetScale(float uiScale)
    {
        return Math.Max(1, (int) MathF.Round(2f * uiScale));
    }

    /// <summary>
    /// Fills a rectangle with one colour.
    /// </summary>
    public void Rect(int x, int y, int width, int height, Color color)
    {
        if (width > 0 && height > 0)
            _handle.DrawRect(Box(x, y, width, height), color);
    }

    /// <summary>
    /// Draws a whole texture, one texel per art pixel.
    /// </summary>
    public void Texture(Texture texture, int x, int y, Color? modulate = null)
    {
        _handle.DrawTextureRect(texture, Box(x, y, texture.Width, texture.Height), modulate);
    }

    /// <summary>
    /// Draws a part of a texture, one texel per art pixel.
    /// </summary>
    public void Region(Texture texture, UIBox2 source, int x, int y, Color? modulate = null)
    {
        Stretch(texture, source, x, y, (int) source.Width, (int) source.Height, modulate);
    }

    /// <summary>
    /// Draws a part of a texture stretched over the given size in art pixels.
    /// </summary>
    public void Stretch(Texture texture, UIBox2 source, int x, int y, int width, int height, Color? modulate = null)
    {
        if (width > 0 && height > 0)
            _handle.DrawTextureRectRegion(texture, Box(x, y, width, height), source, modulate);
    }

    /// <summary>
    /// Draws a frame of any size from a part of a texture: the corners keep their size, the edges and the middle stretch.
    /// </summary>
    public void NineSlice(Texture texture, UIBox2 frame, int left, int top, int right, int bottom, int x, int y, int width, int height, Color? modulate = null)
    {
        for (var row = 0; row < 3; row++)
        {
            var (sourceY, sourceHeight, offsetY, sliceHeight) = Slice(row, top, bottom, (int) frame.Height, height);
            for (var column = 0; column < 3; column++)
            {
                var (sourceX, sourceWidth, offsetX, sliceWidth) = Slice(column, left, right, (int) frame.Width, width);
                var source = UIBox2.FromDimensions(frame.Left + sourceX, frame.Top + sourceY, sourceWidth, sourceHeight);
                Stretch(texture, source, x + offsetX, y + offsetY, sliceWidth, sliceHeight, modulate);
            }
        }
    }

    /// <summary>
    /// Draws one line of text with its top left corner at the given point.
    /// </summary>
    public void Text(PixelFont font, int x, int y, ReadOnlySpan<char> text, Color color)
    {
        var position = new Vector2(x * Scale, y * Scale);
        _handle.DrawString(font.Font, position, text, font.Scale * Scale, color);
    }

    /// <summary>
    /// Draws a seven-segment readout from a strip of white digit cells. Unlit segments stay visible in the ghost colour.
    /// </summary>
    public void Segments(Texture cells, int x, int y, ReadOnlySpan<char> text, Color lit, Color ghost)
    {
        foreach (var character in text)
        {
            if (character == ':')
            {
                Region(cells, Cell(ColonCell), x - 2, y, lit);
                x += ColonAdvance;
                continue;
            }

            Region(cells, Cell(GhostDigit), x, y, ghost);
            if (character is >= '0' and <= '9')
                Region(cells, Cell(character - '0'), x, y, lit);

            x += DigitAdvance;
        }
    }

    /// <summary>
    /// Draws an entity's sprite centred on the given point.
    /// </summary>
    /// <param name="spriteScale">Art pixels per pixel of the sprite, separately across and down.</param>
    public void Entity(EntityUid uid, SpriteComponent sprite, int centerX, int centerY, Vector2 spriteScale, Direction direction = Direction.South)
    {
        var position = new Vector2(centerX * Scale, centerY * Scale);
        _handle.DrawEntity(uid, position, spriteScale * Scale, Angle.Zero, overrideDirection: direction, sprite: sprite);
    }

    /// <summary>
    /// Draws a line one art pixel wide. A thick line is drawn with a plus-shaped brush three art pixels across.
    /// </summary>
    public void Line(int x0, int y0, int x1, int y1, Color color, bool thick = false)
    {
        var deltaX = Math.Abs(x1 - x0);
        var deltaY = -Math.Abs(y1 - y0);
        var stepX = x0 < x1 ? 1 : -1;
        var stepY = y0 < y1 ? 1 : -1;
        var error = deltaX + deltaY;
        while (true)
        {
            if (thick)
            {
                Rect(x0 - 1, y0, 3, 1, color);
                Rect(x0, y0 - 1, 1, 3, color);
            }
            else
            {
                Rect(x0, y0, 1, 1, color);
            }

            if (x0 == x1 && y0 == y1)
                break;

            var doubled = 2 * error;
            if (doubled >= deltaY)
            {
                error += deltaY;
                x0 += stepX;
            }

            if (doubled <= deltaX)
            {
                error += deltaX;
                y0 += stepY;
            }
        }
    }

    private static UIBox2 Cell(int index)
    {
        return UIBox2.FromDimensions(index * DigitWidth, 0f, DigitWidth, DigitHeight);
    }

    private static (int SourceStart, int SourceSize, int Start, int Size) Slice(int index, int near, int far, int source, int size)
    {
        return index switch
        {
            0 => (0, near, 0, near),
            1 => (near, source - near - far, near, size - near - far),
            _ => (source - far, far, size - far, far),
        };
    }

    private UIBox2 Box(int x, int y, int width, int height)
    {
        return UIBox2.FromDimensions(x * Scale, y * Scale, width * Scale, height * Scale);
    }
}
