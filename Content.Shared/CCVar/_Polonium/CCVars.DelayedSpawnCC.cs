using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> DscEnabled =
        CVarDef.Create("dsc.enabled", true, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<string> DscAdminAlert =
        CVarDef.Create("dsc.admin_alert", string.Empty, CVar.SERVERONLY);

    public static readonly CVarDef<string> DscDiscordTitle =
        CVarDef.Create("dsc.discord_title", string.Empty, CVar.SERVERONLY);

    public static readonly CVarDef<string> DscDiscordFieldPlayer =
        CVarDef.Create("dsc.discord_field_player", "Player", CVar.SERVERONLY);

    public static readonly CVarDef<string> DscDiscordFieldDetail =
        CVarDef.Create("dsc.discord_field_detail", "Method", CVar.SERVERONLY);

    public static readonly CVarDef<string> DscDetailEye =
        CVarDef.Create("dsc.detail_eye", string.Empty, CVar.SERVERONLY);

    public static readonly CVarDef<string> DscDetailScale =
        CVarDef.Create("dsc.detail_scale", string.Empty, CVar.SERVERONLY);

    public static readonly CVarDef<string> DscDetailZoom =
        CVarDef.Create("dsc.detail_zoom", string.Empty, CVar.SERVERONLY);

    public static readonly CVarDef<string> DscDetailA =
        CVarDef.Create("dsc.detail_a", string.Empty, CVar.SERVERONLY);

    public static readonly CVarDef<string> DscDetailB =
        CVarDef.Create("dsc.detail_b", string.Empty, CVar.SERVERONLY);

    public static readonly CVarDef<string> DscDetailC =
        CVarDef.Create("dsc.detail_c", string.Empty, CVar.SERVERONLY);

    public static readonly CVarDef<string> DscDetailD =
        CVarDef.Create("dsc.detail_d", string.Empty, CVar.SERVERONLY);

    public static readonly CVarDef<string> DscDetailE =
        CVarDef.Create("dsc.detail_e", string.Empty, CVar.SERVERONLY);
}
