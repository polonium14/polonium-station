using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Polonium.Chat.Emoji;

[Prototype]
public sealed partial class ChatEmojiPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// What players write between colons to use the emoji, e.g. <c>idz</c> for <c>:idz:</c>.
    /// </summary>
    [DataField(required: true)]
    public string Code = string.Empty;

    /// <summary>
    /// The emoji image in its original resolution. The client scales it to the size it draws it at.
    /// </summary>
    [DataField(required: true)]
    public ResPath Sprite;
}
