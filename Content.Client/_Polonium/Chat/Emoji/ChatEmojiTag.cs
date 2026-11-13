using System.Diagnostics.CodeAnalysis;
using Content.Shared._Polonium.Chat.Emoji;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Polonium.Chat.Emoji;

[UsedImplicitly]
public sealed partial class ChatEmojiTag : IMarkupTagHandler
{
    [Dependency] private IPrototypeManager _prototype = default!;

    public string Name => ChatEmojiMarkup.TagName;

    public bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        if (!node.Value.TryGetString(out var id) || !_prototype.TryIndex<ChatEmojiPrototype>(id, out var emoji))
        {
            control = null;
            return false;
        }

        control = new ChatEmojiControl
        {
            Emoji = emoji,
            ToolTip = ChatEmojiMarkup.Shortcode(emoji),
            MouseFilter = Control.MouseFilterMode.Pass,
        };
        return true;
    }
}
