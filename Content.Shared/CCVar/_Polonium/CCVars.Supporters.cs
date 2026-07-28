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
}
