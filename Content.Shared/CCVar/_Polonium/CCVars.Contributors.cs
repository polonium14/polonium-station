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
}
