using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Whether players who have not readied up get a chat reminder before the round starts.
    /// </summary>
    public static readonly CVarDef<bool> LobbyReadyReminderEnabled =
        CVarDef.Create("game.lobby_ready_reminder_enabled", true, CVar.SERVERONLY);

    /// <summary>
    /// Comma-separated seconds before round start at which the reminder is sent, e.g. <c>60,30</c>.
    /// </summary>
    public static readonly CVarDef<string> LobbyReadyReminderTimes =
        CVarDef.Create("game.lobby_ready_reminder_times", "60,30", CVar.SERVERONLY);

    /// <summary>
    /// Whether the reminder also flashes the game window in the taskbar.
    /// </summary>
    public static readonly CVarDef<bool> LobbyReadyReminderFlash =
        CVarDef.Create("game.lobby_ready_reminder_flash", true, CVar.SERVERONLY);

    /// <summary>
    /// Whether the lobby ready button shows how many players are ready, e.g. "Ready (12/19)".
    /// </summary>
    public static readonly CVarDef<bool> LobbyReadyCountOnButton =
        CVarDef.Create("game.lobby_ready_count_on_button", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Whether the lobby countdown gets extended when too few connected players have readied up.
    /// The check happens right before the maps and preset rules are preloaded.
    /// </summary>
    public static readonly CVarDef<bool> LobbyExtendEnabled =
        CVarDef.Create("game.lobby_extend_enabled", true, CVar.SERVERONLY);

    /// <summary>
    /// The countdown is extended when ready players / connected players is below this ratio.
    /// </summary>
    public static readonly CVarDef<float> LobbyExtendReadyRatio =
        CVarDef.Create("game.lobby_extend_ready_ratio", 0.6f, CVar.SERVERONLY);

    /// <summary>
    /// Minimum connected players in the lobby for the extension to apply at all.
    /// </summary>
    public static readonly CVarDef<int> LobbyExtendMinPlayers =
        CVarDef.Create("game.lobby_extend_min_players", 5, CVar.SERVERONLY);

    /// <summary>
    /// How many seconds a single extension adds to the countdown.
    /// </summary>
    public static readonly CVarDef<int> LobbyExtendSeconds =
        CVarDef.Create("game.lobby_extend_seconds", 60, CVar.SERVERONLY);

    /// <summary>
    /// How many times the countdown may be extended in a single lobby.
    /// </summary>
    public static readonly CVarDef<int> LobbyExtendMaxTimes =
        CVarDef.Create("game.lobby_extend_max_times", 1, CVar.SERVERONLY);
}
