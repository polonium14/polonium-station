using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.DiscordLinking;

/// <summary>
/// What the server knows about the Discord link of a player, or why it could not find out.
/// </summary>
[Serializable, NetSerializable]
public enum DiscordLinkStatus : byte
{
    NotLinked,
    Linked,

    /// <summary>
    /// Linking has started, the player finishes it in the browser.
    /// </summary>
    Started,

    /// <summary>
    /// Guest accounts can't be linked.
    /// </summary>
    NotLoggedIn,

    /// <summary>
    /// Linking is switched off or the bot could not be reached.
    /// </summary>
    Unavailable,

    /// <summary>
    /// The player asked again too soon.
    /// </summary>
    Cooldown,
}

/// <summary>
/// Asks the server about the Discord link of the player, or to start linking.
/// </summary>
[Serializable, NetSerializable]
public sealed class DiscordLinkRequestEvent : EntityEventArgs
{
    /// <summary>
    /// Start linking instead of only asking about the link.
    /// </summary>
    public bool Start;

    public DiscordLinkRequestEvent(bool start)
    {
        Start = start;
    }
}

/// <summary>
/// The Discord link of the player as the server knows it.
/// </summary>
[Serializable, NetSerializable]
public sealed class DiscordLinkStateEvent : EntityEventArgs
{
    public DiscordLinkStatus Status;

    /// <summary>
    /// Name of the linked Discord account, e.g. "@nick". Null when it is not linked or the bot did not know the name.
    /// </summary>
    public string? DiscordName;

    /// <summary>
    /// The page in the browser where the player finishes linking. Only set when <see cref="Status"/> is <see cref="DiscordLinkStatus.Started"/>.
    /// </summary>
    public string? Url;

    public DiscordLinkStateEvent(DiscordLinkStatus status, string? discordName = null, string? url = null)
    {
        Status = status;
        DiscordName = discordName;
        Url = url;
    }
}

public static class SharedDiscordLink
{
    /// <summary>
    /// Key of the connection denial property with the URL where a player who has to link Discord starts linking.
    /// </summary>
    public const string DenyUrlProperty = "discord_link_url";
}
