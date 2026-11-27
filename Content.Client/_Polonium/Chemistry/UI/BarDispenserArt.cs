namespace Content.Client._Polonium.Chemistry.UI;

/// <summary>
/// Measurements of the bar dispenser picture in art pixels, shared by the parts of its window.
/// </summary>
public static class BarDispenserArt
{
    public const string TexturePath = "/Textures/_Polonium/Interface/BarDispenser/";

    public const int BodyLeft = 4;
    public const int BodyWidth = 266;
    public const int Top = 2;
    public const int ShelfHeight = 31;
    public const int RailHeight = 6;
    public const int LidHeight = 13;

    public const int KeyWidth = 82;
    public const int KeyHeight = 12;
    public const int KeyColumns = 3;
    public const int KeyPitchX = 84;
    public const int KeyPitchY = 12;
    public const int MaxKeyRows = 9;

    public const int BandHeight = 76;
    public const int BayWidth = 78;
    public const int BayHeight = 68;

    /// <summary>
    /// Art pixels per pixel of the sprite of the vessel under the nozzle.
    /// </summary>
    public const int VesselScale = 2;

    /// <summary>
    /// The animations of the device move in steps, this many a second.
    /// </summary>
    public const float FrameRate = 8f;

    public static readonly Color Ink = Color.FromHex("#0c0c0c");
    public static readonly Color Amber = Color.FromHex("#ffb000");
    public static readonly Color LampGreen = Color.FromHex("#44ff00");

    public static readonly UIBox2 RoundKey = Cell(0, 0, 19, 19);
    public static readonly UIBox2 RoundKeyDown = Cell(20, 0, 19, 19);
    public static readonly UIBox2 DrainIcon = Cell(40, 0, 9, 9);
    public static readonly UIBox2 EjectIcon = Cell(50, 0, 9, 9);
    public static readonly UIBox2 ServiceIcon = Cell(60, 0, 11, 8);
    public static readonly UIBox2 CloseIcon = Cell(72, 0, 7, 7);

    /// <summary>
    /// Rounds the way the picture was drawn: a half always goes up.
    /// </summary>
    public static int Round(float value)
    {
        return (int) MathF.Floor(value + 0.5f);
    }

    /// <summary>
    /// Which step of an animation is on show after the given time.
    /// </summary>
    public static int Frame(float seconds)
    {
        return (int) MathF.Floor(seconds * FrameRate);
    }

    /// <summary>
    /// The colour moved towards white by the given share.
    /// </summary>
    public static Color Lighten(Color color, float share)
    {
        return new Color(
            color.R + (1f - color.R) * share,
            color.G + (1f - color.G) * share,
            color.B + (1f - color.B) * share);
    }

    private static UIBox2 Cell(int x, int y, int width, int height)
    {
        return UIBox2.FromDimensions(x, y, width, height);
    }
}
