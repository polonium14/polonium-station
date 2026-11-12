using System.Linq;
using Content.Client.Lobby;
using Content.Shared._Polonium.Chat.Emoji;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Content.Client._Polonium.Chat.Emoji;

public sealed partial class ChatEmojiUIController : UIController, IOnStateEntered<LobbyState>, IOnStateExited<LobbyState>
{
    private static readonly TimeSpan UnusedTextureLifetime = TimeSpan.FromSeconds(30);

    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IResourceManager _resources = default!;

    private readonly Dictionary<string, Image<Rgba32>> _images = new();
    private readonly Dictionary<(string Id, int Size), CachedTexture> _textures = new();
    private TimeSpan _nextCleanup;
    private ChatEmojiButton? _button;

    public IEnumerable<ChatEmojiPrototype> Emojis =>
        _prototype.EnumeratePrototypes<ChatEmojiPrototype>().OrderBy(emoji => emoji.Code, StringComparer.Ordinal);

    public void OnStateEntered(LobbyState state)
    {
        if (state.Lobby is not { } lobby)
            return;

        var input = lobby.Chat.ChatInput.Input;
        if (_button?.Parent == input.Parent)
            return;

        _button = new ChatEmojiButton(input);
        input.Parent!.AddChild(_button);
        _button.SetPositionInParent(input.GetPositionInParent() + 1);
    }

    public void OnStateExited(LobbyState state)
    {
        _button?.Popup.Close();
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_timing.RealTime < _nextCleanup)
            return;

        _nextCleanup = _timing.RealTime + UnusedTextureLifetime;
        foreach (var (key, cached) in _textures)
        {
            if (_timing.RealTime - cached.LastUsed < UnusedTextureLifetime)
                continue;

            cached.Texture.Dispose();
            _textures.Remove(key);
        }
    }

    public bool StartsWithEmojiCode(string text)
    {
        if (!text.StartsWith(':'))
            return false;

        var end = text.IndexOf(':', 1);
        if (end < 0)
            return false;

        // Ignoring case, because the chat lowercases the input when it picks the channel to show.
        var code = text.AsSpan(1, end - 1);
        foreach (var emoji in _prototype.EnumeratePrototypes<ChatEmojiPrototype>())
        {
            if (code.Equals(emoji.Code, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the emoji resampled to fit a square of the given size in pixels.
    /// Drawing a big image scaled down on the GPU looks grainy, so each size gets its own texture.
    /// </summary>
    public Texture GetTexture(ChatEmojiPrototype emoji, int size)
    {
        if (!_textures.TryGetValue((emoji.ID, size), out var cached))
        {
            cached = new CachedTexture(LoadTexture(emoji, size));
            _textures.Add((emoji.ID, size), cached);
        }

        cached.LastUsed = _timing.RealTime;
        return cached.Texture;
    }

    private OwnedTexture LoadTexture(ChatEmojiPrototype emoji, int size)
    {
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
        return _clyde.LoadTextureFromImage(resized, $"chat emoji {emoji.ID} {size}px");
    }

    private sealed class CachedTexture(OwnedTexture texture)
    {
        public readonly OwnedTexture Texture = texture;
        public TimeSpan LastUsed;
    }
}
