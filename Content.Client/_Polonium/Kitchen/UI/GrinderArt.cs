using System.Numerics;

namespace Content.Client._Polonium.Kitchen.UI;

/// <summary>
/// Measurements of the blender picture in art pixels, shared by the parts of the grinder window.
/// </summary>
public static class GrinderArt
{
    public const string TexturePath = "/Textures/_Polonium/Interface/Grinder/";

    public const int CanvasWidth = 340;
    public const int CanvasHeight = 304;

    public const int JugLeft = 38;
    public const int JugTop = 50;
    public const int JugWidth = 140;
    public const int JugHeight = 104;
    public const int JugTaper = 14;
    public const int JugFloor = 100;
    public const int JugCenter = 70;

    public const int SpoutX = 108;
    public const int SpoutBottom = 232;
    public const int TrayTop = 280;

    /// <summary>
    /// Art pixels per pixel of an entity's sprite.
    /// </summary>
    public const int SpriteScale = 2;

    public static readonly Color LampGreen = Color.FromHex("#44ff00");

    public static readonly UIBox2 KnobOff = Cell(0, 0, 27, 27);
    public static readonly UIBox2 KnobGrind = Cell(28, 0, 27, 27);
    public static readonly UIBox2 KnobJuice = Cell(56, 0, 27, 27);
    public static readonly UIBox2 LargeKey = Cell(84, 0, 25, 25);
    public static readonly UIBox2 LargeKeyDown = Cell(110, 0, 25, 25);
    public static readonly UIBox2 MediumKey = Cell(0, 28, 21, 21);
    public static readonly UIBox2 MediumKeyDown = Cell(22, 28, 21, 21);
    public static readonly UIBox2 SmallKey = Cell(44, 28, 17, 17);
    public static readonly UIBox2 SmallKeyDown = Cell(62, 28, 17, 17);
    public static readonly UIBox2 LampOff = Cell(80, 28, 9, 9);
    public static readonly UIBox2 LampGreenCell = Cell(90, 28, 9, 9);
    public static readonly UIBox2 LampRed = Cell(100, 28, 9, 9);
    public static readonly UIBox2 GrindIcon = Cell(110, 28, 11, 11);
    public static readonly UIBox2 JuiceIcon = Cell(122, 28, 9, 11);
    public static readonly UIBox2 ChamberIcon = Cell(0, 50, 11, 11);
    public static readonly UIBox2 EjectIcon = Cell(12, 50, 9, 9);
    public static readonly UIBox2 CloseIcon = Cell(22, 50, 7, 7);
    public static readonly UIBox2 GrindMark = Cell(30, 50, 7, 7);
    public static readonly UIBox2 JuiceMark = Cell(38, 50, 5, 7);

    /// <summary>
    /// Rounds the way the picture was drawn: a half always goes up.
    /// </summary>
    public static int Round(float value)
    {
        return (int) MathF.Floor(value + 0.5f);
    }

    /// <summary>
    /// How far the slanted glass wall stands in from the edge of the jug at the given row.
    /// </summary>
    public static int Inset(int row)
    {
        return Round(JugTaper * Math.Clamp(row, 0, JugHeight) / (float) JugHeight);
    }

    /// <summary>
    /// How deep a column of the jug goes before it meets the slanted wall.
    /// </summary>
    public static int ColumnFloor(int column)
    {
        var fromWall = Math.Min(column, JugWidth - 1 - column);
        return Math.Min(JugFloor, (int) MathF.Floor((fromWall + 0.5f) * JugHeight / JugTaper));
    }

    /// <summary>
    /// A colour as three numbers from 0 to 255, the form the mass and its crumbs are mixed in.
    /// </summary>
    public static Vector3 Rgb(Color color)
    {
        return new Vector3(color.R, color.G, color.B) * 255f;
    }

    /// <summary>
    /// The colour that three numbers from 0 to 255 stand for.
    /// </summary>
    public static Color ToColor(Vector3 rgb, float alpha = 1f)
    {
        return new Color(Channel(rgb.X), Channel(rgb.Y), Channel(rgb.Z), (byte) (Math.Clamp(alpha, 0f, 1f) * 255f));
    }

    /// <summary>
    /// One part of a colour as a byte, cut to the range a byte holds.
    /// </summary>
    public static byte Channel(float value)
    {
        return (byte) Math.Clamp(value, 0f, 255f);
    }

    private static UIBox2 Cell(int x, int y, int width, int height)
    {
        return UIBox2.FromDimensions(x, y, width, height);
    }
}
