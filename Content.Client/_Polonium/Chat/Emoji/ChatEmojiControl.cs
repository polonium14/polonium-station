using System.Numerics;
using Content.Shared._Polonium.Chat.Emoji;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Polonium.Chat.Emoji;

/// <summary>
/// Draws a chat emoji. Without a set size it is as tall as a line of its parent's text, so it fits inline in chat.
/// </summary>
public sealed class ChatEmojiControl : Control
{
    public ChatEmojiPrototype? Emoji { get; set; }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var font = Parent != null && Parent.TryGetStyleProperty<Font>(Label.StylePropertyFont, out var parentFont)
            ? parentFont
            : UserInterfaceManager.ThemeDefaults.DefaultFont;

        var size = font.GetLineHeight(UIScale) / UIScale;
        return new Vector2(size, size);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var size = Math.Min(PixelWidth, PixelHeight);
        if (Emoji == null || size <= 0)
            return;

        var texture = UserInterfaceManager.GetUIController<ChatEmojiUIController>().GetTexture(Emoji, size);
        handle.DrawTexture(texture, (PixelSize - texture.Size) / 2);
    }
}
