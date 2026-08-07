using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> ChatAutoBanEnabled =
        CVarDef.Create("shutup.enabled", false, CVar.SERVERONLY);

    /// <summary>
    /// If a chat message contains any of them the sender is autobanned.
    /// Empty disables matching. Example: "foo,bar,baz"
    /// </summary>
    public static readonly CVarDef<string> ChatAutoBanFragment =
        CVarDef.Create("shutup.auto_ban_fragment", string.Empty, CVar.SERVERONLY);

    /// <summary>
    /// Duration of the autoban in minutes. 0 means perma.
    /// </summary>
    public static readonly CVarDef<int> ChatAutoBanDurationMinutes =
        CVarDef.Create("shutup.auto_ban_duration_minutes", 1, CVar.SERVERONLY);

    /// <summary>
    /// Discord channel ID for autoban logs.
    /// </summary>
    public static readonly CVarDef<string> ChatAutoBanDiscordChannelId =
        CVarDef.Create("shutup.auto_ban_discord_channel_id", string.Empty, CVar.SERVERONLY);
}
