using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Oxygen saturation a body with no working heart loses per second, until it has none left.
    /// The default empties a full human in about 14 seconds; from there it suffocates at the usual rate.
    /// </summary>
    public static readonly CVarDef<float> OrgansNoHeartSaturationLoss =
        CVarDef.Create("organs.no_heart_saturation_loss", 0.5f, CVar.SERVERONLY);
}
