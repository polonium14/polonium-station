using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Whether players can pick a ghost theme. When off, every ghost looks like the default one.
    /// </summary>
    public static readonly CVarDef<bool> GhostThemeEnabled =
        CVarDef.Create("ghost_theme.enabled", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// The ghost theme the player picked. Empty for the default ghost.
    /// </summary>
    public static readonly CVarDef<string> GhostThemeSelected =
        CVarDef.Create("ghost_theme.selected", string.Empty, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// The color the player picked for a ghost theme that can be recolored, as a hex string.
    /// </summary>
    public static readonly CVarDef<string> GhostThemeColor =
        CVarDef.Create("ghost_theme.color", "#FFFFFF", CVar.CLIENTONLY | CVar.ARCHIVE);
}
