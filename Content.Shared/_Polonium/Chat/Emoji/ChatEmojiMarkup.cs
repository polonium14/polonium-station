using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Chat.Emoji;

public static class ChatEmojiMarkup
{
    public const string TagName = "emoji";

    public static string Shortcode(ChatEmojiPrototype emoji)
    {
        return $":{emoji.Code}:";
    }

    public static string Tag(ProtoId<ChatEmojiPrototype> emoji)
    {
        return $"[{TagName}=\"{emoji.Id}\"/]";
    }
}
