using System.Numerics;
using Content.Client.UserInterface.Systems.Chat.Controls;
using Content.Shared._Polonium.Chat.Emoji;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Chat.Emoji;

public sealed class ChatEmojiButton : ChatPopupButton<ChatEmojiPicker>
{
    private const string IconPath = "/Textures/_Polonium/Interface/Chat/emoji_button.png";
    private const float PopupGap = 4;

    private static readonly Color ColorNormal = Color.FromHex("#7b7e9e");
    private static readonly Color ColorHovered = Color.FromHex("#9699bb");
    private static readonly Color ColorPressed = Color.FromHex("#789B8C");

    private readonly LineEdit _input;
    private readonly TextureRect _icon;

    public ChatEmojiButton(LineEdit input)
    {
        _input = input;

        StyleBoxOverride = new StyleBoxEmpty();
        MinWidth = 24;
        ToolTip = Loc.GetString("chat-emoji-button-tooltip");

        _icon = new TextureRect
        {
            TexturePath = IconPath,
            TextureScale = new Vector2(0.5f),
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
        };
        AddChild(_icon);
        UpdateIconColor();

        Popup.OnEmojiPicked += OnEmojiPicked;
    }

    protected override UIBox2 GetPopupPosition()
    {
        return UIBox2.FromDimensions(GetPopupOrigin(), Vector2.Zero);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!Popup.Visible)
            return;

        var origin = GetPopupOrigin();
        if (PopupContainer.GetPopupOrigin(Popup) != origin)
            PopupContainer.SetPopupOrigin(Popup, origin);
    }

    private Vector2 GetPopupOrigin()
    {
        // A closed picker measures as zero, so the position right after opening is fixed in the next FrameUpdate.
        Popup.Measure(Vector2Helpers.Infinity);
        var size = Popup.DesiredSize;
        var x = GlobalPosition.X + Size.X - size.X;
        var y = GlobalPosition.Y - size.Y - PopupGap;

        if (y < 0)
            y = GlobalPosition.Y + Size.Y + PopupGap;

        return new Vector2(x, y);
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();
        UpdateIconColor();
    }

    private void UpdateIconColor()
    {
        // The base constructor calls DrawModeChanged before the icon exists.
        if (_icon == null)
            return;

        _icon.ModulateSelfOverride = DrawMode switch
        {
            DrawModeEnum.Pressed => ColorPressed,
            DrawModeEnum.Hover => ColorHovered,
            _ => ColorNormal,
        };
    }

    private void OnEmojiPicked(ChatEmojiPrototype emoji)
    {
        _input.InsertAtCursor(ChatEmojiMarkup.Shortcode(emoji));

        if (!Popup.Visible)
            _input.GrabKeyboardFocus();
    }
}
