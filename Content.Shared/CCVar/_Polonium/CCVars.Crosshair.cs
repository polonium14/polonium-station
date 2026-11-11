using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Whether every gun shows the default crosshair instead of the one it picks itself.
    /// </summary>
    public static readonly CVarDef<bool> CrosshairSingle =
        CVarDef.Create("crosshair.single", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Colour of the gun crosshair, as a hex string.
    /// </summary>
    public static readonly CVarDef<string> CrosshairColor =
        CVarDef.Create("crosshair.color", "#FFFFFFE6", CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Size multiplier of the gun crosshair on top of the UI scale.
    /// </summary>
    public static readonly CVarDef<float> CrosshairScale =
        CVarDef.Create("crosshair.scale", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether the gun crosshair has a dark outline.
    /// </summary>
    public static readonly CVarDef<bool> CrosshairOutline =
        CVarDef.Create("crosshair.outline", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether the gun crosshair eases into a new spread instead of jumping to it.
    /// </summary>
    public static readonly CVarDef<bool> CrosshairSmoothing =
        CVarDef.Create("crosshair.smoothing", true, CVar.CLIENTONLY | CVar.ARCHIVE);
}
