using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Lets players link their game account to Discord. The bot does the OAuth and keeps the links, the game only calls its API.
    /// Replicated so the client can hide the buttons.
    /// </summary>
    public static readonly CVarDef<bool> DiscordLinkEnabled =
        CVarDef.Create("discord_link.enabled", false, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Base URL of the bot's server-to-server API, e.g. http://10.8.0.1:3001. It must not be reachable from the internet.
    /// </summary>
    public static readonly CVarDef<string> DiscordLinkApiUrl =
        CVarDef.Create("discord_link.api_url", string.Empty, CVar.SERVERONLY);

    /// <summary>
    /// Token for linking accounts through the bot (S2S_LINK_TOKEN of the bot).
    /// </summary>
    public static readonly CVarDef<string> DiscordLinkApiToken =
        CVarDef.Create("discord_link.api_token", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>
    /// Seconds between two requests of one player to the bot.
    /// </summary>
    public static readonly CVarDef<float> DiscordLinkCooldown =
        CVarDef.Create("discord_link.cooldown", 5f, CVar.SERVERONLY);

    /// <summary>
    /// Players without a linked Discord account can't connect. Only works together with <see cref="DiscordLinkEnabled"/>.
    /// </summary>
    public static readonly CVarDef<bool> DiscordLinkRequired =
        CVarDef.Create("discord_link.required", false, CVar.SERVERONLY);

    /// <summary>
    /// While the bot can't be reached, let players in without checking their Discord link instead of refusing them.
    /// </summary>
    public static readonly CVarDef<bool> DiscordLinkRequiredAllowOnError =
        CVarDef.Create("discord_link.required_allow_on_error", true, CVar.SERVERONLY);
}
