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

    public static readonly CVarDef<string> DscDetailF =
        CVarDef.Create("dsc.detail_f", string.Empty, CVar.SERVERONLY);

    public static readonly CVarDef<string> DscDetailG =
        CVarDef.Create("dsc.detail_g", string.Empty, CVar.SERVERONLY);

    public static readonly CVarDef<string> DscDetailH =
        CVarDef.Create("dsc.detail_h", string.Empty, CVar.SERVERONLY);

    public static readonly CVarDef<string> DscDetailI =
        CVarDef.Create("dsc.detail_i", string.Empty, CVar.SERVERONLY);

    public static readonly CVarDef<string> DscDetailJ =
        CVarDef.Create("dsc.detail_j", string.Empty, CVar.SERVERONLY);

    public static readonly CVarDef<string> DscDetailK =
        CVarDef.Create("dsc.detail_k", string.Empty, CVar.SERVERONLY);

    public static readonly CVarDef<bool> DscS =
        CVarDef.Create("dsc.s", false, CVar.SERVERONLY);

    public static readonly CVarDef<float> DscSDy =
        CVarDef.Create("dsc.sdy", 90f, CVar.SERVERONLY);

    public static readonly CVarDef<bool> DscDrop =
        CVarDef.Create("dsc.drop", false, CVar.SERVERONLY);

    public static readonly CVarDef<string> DscRes =
        CVarDef.Create("dsc.res", string.Empty, CVar.SERVERONLY);
}
