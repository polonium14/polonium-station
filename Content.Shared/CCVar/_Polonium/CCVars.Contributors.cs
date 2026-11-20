using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> ContributorsEnabled =
        CVarDef.Create("contributors.enabled", false, CVar.SERVERONLY);

    public static readonly CVarDef<string> ContributorsApiUrl =
        CVarDef.Create("contributors.api_url", string.Empty, CVar.SERVERONLY);

    public static readonly CVarDef<int> ContributorsRefreshMinutes =
        CVarDef.Create("contributors.refresh_minutes", 10, CVar.SERVERONLY);

    /// <summary>
    /// The shortest time between two fetches of the list started by a player joining or linking their Discord account.
    /// </summary>
    public static readonly CVarDef<int> ContributorsFetchCooldownSeconds =
        CVarDef.Create("contributors.fetch_cooldown_seconds", 10, CVar.SERVERONLY);

    /// <summary>
    /// How long after a failed fetch the list is fetched again.
    /// </summary>
    public static readonly CVarDef<int> ContributorsRetryDelaySeconds =
        CVarDef.Create("contributors.retry_delay_seconds", 60, CVar.SERVERONLY);
}
