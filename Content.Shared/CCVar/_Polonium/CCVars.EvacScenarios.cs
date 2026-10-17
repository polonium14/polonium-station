using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Whether evac flight scenarios get rolled when the emergency shuttle leaves.
    /// A scenario forced by an admin with the <c>evacscenario</c> command runs either way.
    /// </summary>
    public static readonly CVarDef<bool> EvacScenariosEnabled =
        CVarDef.Create("evac.scenarios_enabled", true, CVar.SERVERONLY);
}
