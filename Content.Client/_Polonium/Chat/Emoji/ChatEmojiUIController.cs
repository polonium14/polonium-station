using System.Linq;
using Content.Client.Lobby;
using Content.Client.UserInterface.Systems.Chat.Widgets;
using Content.Shared._Polonium.Chat.Emoji;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Content.Client._Polonium.Chat.Emoji;

public sealed partial class ChatEmojiUIController : UIController, IOnStateEntered<LobbyState>
{
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IResourceManager _resources = default!;

    private readonly Dictionary<string, Image<Rgba32>> _images = new();
    private readonly Dictionary<(string Id, int Size), Texture> _textures = new();
    private ChatBox? _lobbyChat;

    public IEnumerable<ChatEmojiPrototype> Emojis =>
        _prototype.EnumeratePrototypes<ChatEmojiPrototype>().OrderBy(emoji => emoji.Code, StringComparer.Ordinal);

    public void OnStateEntered(LobbyState state)
    {
        if (state.Lobby is not { } lobby || lobby.Chat == _lobbyChat)
            return;

        _lobbyChat = lobby.Chat;

        var input = lobby.Chat.ChatInput.Input;
        var button = new ChatEmojiButton(input);
        input.Parent!.AddChild(button);
        button.SetPositionInParent(input.GetPositionInParent() + 1);
    }

    /// <summary>
    /// Returns the emoji resampled to fit a square of the given size in pixels.
    /// Drawing a big image scaled down on the GPU looks grainy, so each size gets its own texture.
    /// </summary>
    public Texture GetTexture(ChatEmojiPrototype emoji, int size)
    {
        if (_textures.TryGetValue((emoji.ID, size), out var texture))
            return texture;

        if (!_images.TryGetValue(emoji.ID, out var image))
        {
            using var stream = _resources.ContentFileRead(emoji.Sprite);
            image = Image.Load<Rgba32>(stream);
            _images.Add(emoji.ID, image);
        }

        var scale = (float) size / Math.Max(image.Width, image.Height);
        var width = Math.Max(1, (int) MathF.Round(image.Width * scale));
        var height = Math.Max(1, (int) MathF.Round(image.Height * scale));

        using var resized = image.Clone(context => context.Resize(width, height));
        texture = _clyde.LoadTextureFromImage(resized, $"chat emoji {emoji.ID} {size}px");
        _textures.Add((emoji.ID, size), texture);
        return texture;
    }
}
