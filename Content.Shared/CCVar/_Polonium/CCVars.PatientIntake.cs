using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Whether patient intake machines deliver patients at all.
    /// </summary>
    public static readonly CVarDef<bool> PatientIntakeEnabled =
        CVarDef.Create("patient_intake.enabled", true, CVar.SERVERONLY);

    /// <summary>
    /// Minutes after round start before the first case.
    /// </summary>
    public static readonly CVarDef<float> PatientIntakeFirstDelay =
        CVarDef.Create("patient_intake.first_delay", 10f, CVar.SERVERONLY);

    /// <summary>
    /// Minimum minutes between the end of one case and the next.
    /// </summary>
    public static readonly CVarDef<float> PatientIntakeGapMin =
        CVarDef.Create("patient_intake.gap_min", 5f, CVar.SERVERONLY);

    /// <summary>
    /// Maximum minutes between the end of one case and the next.
    /// </summary>
    public static readonly CVarDef<float> PatientIntakeGapMax =
        CVarDef.Create("patient_intake.gap_max", 10f, CVar.SERVERONLY);
}
