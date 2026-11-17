using System.Numerics;
using Content.Client.Stylesheets;
using Content.Shared._Polonium.GhostThemes;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._Polonium.GhostThemes;

/// <summary>
/// A row in the ghost theme window: the theme's icon, name and how long it takes to unlock.
/// </summary>
public sealed class GhostThemeEntry : ContainerButton
{
    private static readonly Color LockedModulate = Color.White.WithAlpha(0.45f);

    /// <summary>
    /// The theme this row shows.
    /// </summary>
    public readonly ProtoId<GhostThemePrototype> ThemeId;

    public GhostThemeEntry(GhostThemePrototype theme, Texture icon, Texture lockIcon, bool locked)
    {
        ThemeId = theme.ID;
        ToggleMode = true;
        AddStyleClass(StyleClassButton);

        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            Margin = new Thickness(4, 2),
        };

        // Icons are 32x32 or 64x64, so they always scale by a whole number.
        box.AddChild(new TextureRect
        {
            Texture = icon,
            MinSize = new Vector2(64, 64),
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            VerticalAlignment = VAlignment.Center,
            Modulate = locked ? LockedModulate : Color.White,
        });

        box.AddChild(new Label
        {
            Text = Loc.GetString(theme.Name),
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center,
            Modulate = locked ? LockedModulate : Color.White,
        });

        if (theme.Playtime > TimeSpan.Zero)
        {
            var playtime = new Label
            {
                Text = Loc.GetString("ghost-theme-entry-playtime", ("hours", (int) theme.Playtime.TotalHours)),
                VerticalAlignment = VAlignment.Center,
            };
            playtime.AddStyleClass(StyleClass.LabelWeak);
            box.AddChild(playtime);
        }

        if (locked)
        {
            box.AddChild(new TextureRect
            {
                Texture = lockIcon,
                SetSize = new Vector2(16, 16),
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
                VerticalAlignment = VAlignment.Center,
            });
        }

        AddChild(box);
    }
}
