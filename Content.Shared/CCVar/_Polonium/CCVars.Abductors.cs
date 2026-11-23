using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Whether the abductors can be picked by the event schedulers. Admins can start them either way.
    /// </summary>
    public static readonly CVarDef<bool> AbductorsEnabled =
        CVarDef.Create("abductors.enabled", false, CVar.SERVERONLY);
}
