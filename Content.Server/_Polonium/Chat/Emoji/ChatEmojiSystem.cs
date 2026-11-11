using System.Text;
using Content.Shared._Polonium.Chat.Emoji;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Polonium.Chat.Emoji;

public sealed partial class ChatEmojiSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    private readonly Dictionary<string, ChatEmojiPrototype> _byCode = new();
    private int _limit;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        Subs.CVar(_cfg, CCVars.ChatEmojiLimit, value => _limit = value, true);
        CacheCodes();
    }

    /// <summary>
    /// Escapes a player's message for markup and turns codes of known emojis in it into emoji markup.
    /// </summary>
    public string EscapeWithEmojis(string message)
    {
        var escapedText = FormattedMessage.EscapeText(message);

        StringBuilder? builder = null;
        var copied = 0;
        var emojis = 0;
        var start = escapedText.IndexOf(':');

        while (start >= 0 && emojis < _limit)
        {
            var end = escapedText.IndexOf(':', start + 1);
            if (end < 0)
                break;

            if (!_byCode.TryGetValue(escapedText[(start + 1)..end], out var emoji))
            {
                // The closing colon can still open the next code, e.g. in "12:00:idz:".
                start = end;
                continue;
            }

            builder ??= new StringBuilder(escapedText.Length);
            builder.Append(escapedText, copied, start - copied).Append(ChatEmojiMarkup.Tag(emoji.ID));
            emojis++;
            copied = end + 1;
            start = escapedText.IndexOf(':', copied);
        }

        if (builder == null)
            return escapedText;

        return builder.Append(escapedText, copied, escapedText.Length - copied).ToString();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<ChatEmojiPrototype>())
            CacheCodes();
    }

    private void CacheCodes()
    {
        _byCode.Clear();
        foreach (var emoji in _prototype.EnumeratePrototypes<ChatEmojiPrototype>())
        {
            if (!_byCode.TryAdd(emoji.Code, emoji))
                Log.Error($"Chat emojis {_byCode[emoji.Code].ID} and {emoji.ID} have the same code {emoji.Code}");
        }
    }
}
