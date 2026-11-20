using System.Numerics;
using Content.Client.Stylesheets;
using Content.Shared._Polonium.GhostThemes;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Polonium.GhostThemes;

/// <summary>
/// A tile in the ghost theme grid: the theme's icon with marks for a locked theme and the one in use.
/// </summary>
public sealed class GhostThemeTile : ContainerButton
{
    private const float IconSize = 96f;
    private const float Padding = 8f;

    /// <summary>
    /// The width and height of a tile.
    /// </summary>
    public const float TileSize = IconSize + Padding * 2;

    private static readonly Color LockedModulate = Color.White.WithAlpha(0.35f);

    // A plain box keeps the colors of the button style without its wide side margins.
    private static readonly StyleBoxFlat Box = new(Color.White)
    {
        ContentMarginLeftOverride = Padding,
        ContentMarginRightOverride = Padding,
        ContentMarginTopOverride = Padding,
        ContentMarginBottomOverride = Padding,
    };

    /// <summary>
    /// The theme this tile shows.
    /// </summary>
    public readonly GhostThemePrototype GhostTheme;

    /// <summary>
    /// The localized name of the theme, used for search.
    /// </summary>
    public readonly string ThemeName;

    private readonly TextureRect _icon;
    private readonly Control _lock;
    private readonly TextureRect _inUse;

    public GhostThemeTile(GhostThemePrototype theme, Texture icon, Texture lockIcon, Texture inUseIcon, ButtonGroup group)
    {
        GhostTheme = theme;
        ThemeName = Loc.GetString(theme.Name);
        Group = group;
        ToolTip = ThemeName;
        StyleBoxOverride = Box;
        AddStyleClass(StyleClassButton);

        var stack = new Control { SetSize = new Vector2(IconSize, IconSize) };

        // Pixel art only stays sharp when scaled by a whole number.
        var scale = Math.Max(1, (int) (IconSize / Math.Max(icon.Width, icon.Height)));
        _icon = new TextureRect
        {
            Texture = icon,
            TextureScale = new Vector2(scale, scale),
            Stretch = TextureRect.StretchMode.KeepCentered,
        };
        stack.AddChild(_icon);

        _lock = new PanelContainer
        {
            StyleClasses = { StyleClass.PanelDark },
            HorizontalAlignment = HAlignment.Right,
            VerticalAlignment = VAlignment.Bottom,
            Children =
            {
                new TextureRect
                {
                    Texture = lockIcon,
                    SetSize = new Vector2(16, 16),
                    Margin = new Thickness(4),
                    Stretch = TextureRect.StretchMode.KeepAspectCentered,
                },
            },
        };
        stack.AddChild(_lock);

        _inUse = new TextureRect
        {
            Texture = inUseIcon,
            SetSize = new Vector2(20, 20),
            HorizontalAlignment = HAlignment.Left,
            VerticalAlignment = VAlignment.Top,
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
        };
        stack.AddChild(_inUse);

        AddChild(stack);
    }

    /// <summary>
    /// Dims the icon and shows a lock while the player cannot use the theme.
    /// </summary>
    public void SetLocked(bool locked)
    {
        _icon.Modulate = locked ? LockedModulate : Color.White;
        _lock.Visible = locked;
    }

    /// <summary>
    /// Marks the theme the player's ghost wears.
    /// </summary>
    public void SetInUse(bool inUse)
    {
        _inUse.Visible = inUse;
    }
}
