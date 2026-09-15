using Content.Client._Polonium.RichText; // Polonium
using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Controls;

/// <summary>
/// Carries link data parsed and resolved from a TextLink <see cref="MarkupNode"/>,
/// only one field per link should be populated at a time.
/// </summary>
public sealed class TextLinkLabel : LinkLabel // Polonium: underlined like other links
{
    public string? LinkString { get; init; }
    public NetEntity? LinkEntity { get; init; }

    // Polonium: player names in chat stay plain
    protected override bool DrawUnderline => LinkEntity == null;
}
