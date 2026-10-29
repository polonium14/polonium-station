// Port of chat stacking from RMC14 (RMC-14/RMC-14#7587) via forky-station (funky-station/forky-station#421).

using Content.Client._Polonium.RichText;
using Content.Client.RichText;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Utility;

namespace Content.Client._Polonium.Chat;

/// <summary>
/// Merges a repeated chat message into its earlier line with an "xN" counter instead of adding a new line.
/// Each chat box owns its own stacker, because each box has its own lines.
/// </summary>
public sealed class ChatRepeatStacker(IConfigurationManager cfg)
{
    private readonly Queue<Entry> _recent = new();

    /// <summary>
    /// Tries to stack the message onto one of the recent lines in <paramref name="contents"/>.
    /// If it returns false, the caller must add <paramref name="formatted"/> as a new line right away,
    /// because it is remembered under the next line index.
    /// </summary>
    public bool TryStack(OutputPanel contents, FormattedMessage formatted, ChatMessage msg, Color color, bool allowUrl)
    {
        var history = cfg.GetCVar(CCVars.ChatRepeatHistory);

        // Admin messages are never merged, so admins don't miss any of them.
        if (history <= 0 || ChatChannel.AdminRelated.HasFlag(msg.Channel))
            return false;

        // The wrapped message contains the visible speaker name, so different speakers on the radio
        // (which has no sender entity) don't stack, while players can't learn more than they see in chat.
        var key = new RepeatKey(msg.Channel, msg.SenderEntity, msg.WrappedMessage, color, allowUrl);
        foreach (var entry in _recent)
        {
            if (entry.Key != key)
                continue;

            entry.Count++;
            var stacked = new FormattedMessage(entry.Message);
            stacked.AddMarkupPermissive(" " + Loc.GetString("chat-repeat-counter", ("count", entry.Count)));
            SetMessage(contents, entry.Index, stacked, allowUrl);
            return true;
        }

        _recent.Enqueue(new Entry(key, contents.EntryCount, formatted));
        while (_recent.Count > history)
        {
            _recent.Dequeue();
        }

        return false;
    }

    /// <summary>
    /// Forgets all remembered lines. Call it whenever the chat box contents are cleared,
    /// otherwise the remembered line indexes point at wrong lines.
    /// </summary>
    public void Clear()
    {
        _recent.Clear();
    }

    // Same tags as ChatBox.AddLine, so the stacked line renders like the original one.
    private static void SetMessage(OutputPanel contents, int index, FormattedMessage message, bool allowUrl)
    {
        if (allowUrl)
        {
            using (UrlTag.Allow())
                contents.SetMessage(index, message, tagsAllowed: null);
            return;
        }

        contents.SetMessage(index, message, UserFormattableTags.WithoutUrl);
    }

    private readonly record struct RepeatKey(
        ChatChannel Channel,
        NetEntity Sender,
        string WrappedMessage,
        Color Color,
        bool AllowUrl);

    private sealed class Entry(RepeatKey key, int index, FormattedMessage message)
    {
        public readonly RepeatKey Key = key;
        public readonly int Index = index;
        public readonly FormattedMessage Message = message;
        public int Count = 1;
    }
}
