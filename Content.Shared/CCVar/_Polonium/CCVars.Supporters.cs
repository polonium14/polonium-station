using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> SupportersEnabled =
        CVarDef.Create("supporters.enabled", false, CVar.SERVERONLY);

    public static readonly CVarDef<string> SupportersApiUrl =
        CVarDef.Create("supporters.api_url", string.Empty, CVar.SERVERONLY);

    public static readonly CVarDef<string> SupportersApiToken =
        CVarDef.Create("supporters.api_token", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL);

    public static readonly CVarDef<string> SupportersNameColor =
        CVarDef.Create("supporters.name_color", "gold", CVar.SERVERONLY);

    public static readonly CVarDef<float> SupportersRefreshMinutes =
        CVarDef.Create("supporters.refresh_minutes", 60f, CVar.SERVERONLY);

    /// <summary>
    /// The shortest time between two fetches of the list started by a player joining or linking their Discord account.
    /// </summary>
    public static readonly CVarDef<int> SupportersFetchCooldownSeconds =
        CVarDef.Create("supporters.fetch_cooldown_seconds", 10, CVar.SERVERONLY);

    /// <summary>
    /// How long after a failed fetch the list is fetched again.
    /// </summary>
    public static readonly CVarDef<int> SupportersRetryDelaySeconds =
        CVarDef.Create("supporters.retry_delay_seconds", 60, CVar.SERVERONLY);

    /// <summary>
    /// The page where players can become supporters. The game shows no link to it while it is empty.
    /// </summary>
    public static readonly CVarDef<string> SupportersLink =
        CVarDef.Create("supporters.link", string.Empty, CVar.SERVER | CVar.REPLICATED);
}
