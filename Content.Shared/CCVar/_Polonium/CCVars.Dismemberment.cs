using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Multiplier on the chance that a cut severs a badly hurt limb.
    /// </summary>
    public static readonly CVarDef<float> DismembermentChanceMultiplier =
        CVarDef.Create("dismemberment.chance_multiplier", 0.5f, CVar.SERVERONLY);
}
