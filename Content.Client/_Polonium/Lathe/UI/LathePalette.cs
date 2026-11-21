namespace Content.Client._Polonium.Lathe.UI;

public static class LathePalette
{
    public const string TexturePath = "/Textures/_Polonium/Interface/Lathe/";

    /// <summary>
    /// Height of a line of screen text in art pixels.
    /// </summary>
    public const int ScreenRow = 10;

    public const int IconCell = 7;

    public static readonly Color ScreenBack = Color.FromHex("#0a120a");
    public static readonly Color ScreenGrid = Color.FromHex("#102410");
    public static readonly Color ScreenDim = Color.FromHex("#1a551a");
    public static readonly Color ScreenLow = Color.FromHex("#1a7a22");
    public static readonly Color ScreenMid = Color.FromHex("#1fbf2f");
    public static readonly Color Screen = Color.FromHex("#33ff33");
    public static readonly Color ScreenHot = Color.FromHex("#c8ffc8");
    public static readonly Color ScreenInput = Color.FromHex("#a8ffa8");
    public static readonly Color RowHover = Color.FromHex("#14501a");
    public static readonly Color RowHoverOff = Color.FromHex("#0f240f");

    public static readonly Color Outline = Color.FromHex("#0b0c12");
    public static readonly Color Panel = Color.FromHex("#212330");
    public static readonly Color Thumb = Color.FromHex("#606375");
    public static readonly Color Engraved = Color.FromHex("#9497ab");
    public static readonly Color Faint = Color.FromHex("#707387");
    public static readonly Color TrayLabel = Color.FromHex("#474a5b");
    public static readonly Color Label = Color.FromHex("#c5c7d4");
    public static readonly Color PlateText = Color.FromHex("#151824");
    public static readonly Color KeyText = Color.FromHex("#212330");
    public static readonly Color KeyTextDark = Color.FromHex("#dfe0e8");
    public static readonly Color KeyMark = Color.FromHex("#807b7a");

    public static readonly Color Ink = Color.FromHex("#1d2a1d");
    public static readonly Color InkSoft = Color.FromHex("#5f7a58");
    public static readonly Color PaperLine = Color.FromHex("#7f9a78");
    public static readonly Color Stamp = Color.FromHex("#b83325");

    public static UIBox2 Icon(LatheKeyIcon icon)
    {
        return UIBox2.FromDimensions((int) icon * IconCell, 0f, IconCell, IconCell);
    }
}

public enum LatheKeyIcon
{
    Left,
    Right,
    Up,
    Down,
    Cross,
    Plus,
    Minus,
    Mark,
}
