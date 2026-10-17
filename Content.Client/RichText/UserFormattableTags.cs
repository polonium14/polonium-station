using Content.Client.Guidebook.RichText;
using Content.Client.Guidebook.Richtext;
using Content.Client.UserInterface.RichText;
using Robust.Client.UserInterface.RichText;

namespace Content.Client.RichText;

/// <summary>
/// Contains rules for what markup tags are allowed to be used by players.
/// </summary>
public static class UserFormattableTags
{
    /// <summary>
    /// The basic set of "rich text" formatting tags that shouldn't cause any issues.
    /// Limit user rich text to these by default.
    /// </summary>
    public static readonly Type[] BaseAllowedTags =
    [
        typeof(BoldItalicTag),
        typeof(BoldTag),
        typeof(BulletTag),
        typeof(ColorTag),
        typeof(HeadingTag),
        typeof(ItalicTag),
        typeof(MonoTag),
    ];

    /// <summary>
    /// Tags allowed in Silicon UIs. Extends from BaseAllowedTags.
    /// </summary>
    public static readonly Type[] SiliconAllowedTags =
    [
        ..BaseAllowedTags,
        typeof(ScrambleTag)
    ];

    public static readonly Type[] WithoutUrl =
    [
        typeof(BoldItalicTag),
        typeof(BoldTag),
        typeof(BulletTag),
        typeof(ColorTag),
        typeof(CommandLinkTag),
        typeof(FontTag),
        typeof(HeadingTag),
        typeof(ItalicTag),
        typeof(KeyBindTag),
        typeof(MonoTag),
        typeof(ProtodataTag),
        typeof(ScrambleTag),
        typeof(TextLinkTag),
    ];
}
