using Content.Client.ContextMenu.UI;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Administration.UI;

/// <summary>
///     A context-menu entry with a plain texture icon, used by <see cref="AdminEntityResultsList"/> so the row
///     action menu reuses the right-click menu look without needing an entity sprite.
/// </summary>
public sealed class AdminActionMenuElement : ContextMenuElement
{
    public AdminActionMenuElement(string text, Texture? icon) : base(text)
    {
        if (icon != null)
        {
            Icon.AddChild(new TextureRect
            {
                Texture = icon,
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
            });
        }
    }
}
