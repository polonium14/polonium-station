using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Seconds one cycle of chest compressions takes. The cycle repeats until the rescuer stops or moves.
    /// </summary>
    public static readonly CVarDef<float> CprCycleTime =
        CVarDef.Create("cpr.cycle_time", 4f, CVar.SERVERONLY);

    /// <summary>
    /// Asphyxiation healed per cycle on a patient whose heart is still in the body.
    /// </summary>
    public static readonly CVarDef<float> CprAsphyxiationHeal =
        CVarDef.Create("cpr.asphyxiation_heal", 15f, CVar.SERVERONLY);

    /// <summary>
    /// Chance per cycle that the compressions break the ribs.
    /// </summary>
    public static readonly CVarDef<float> CprRibBreakChance =
        CVarDef.Create("cpr.rib_break_chance", 0.05f, CVar.SERVERONLY);

    /// <summary>
    /// Blunt damage the torso takes when the ribs break.
    /// </summary>
    public static readonly CVarDef<float> CprRibBreakDamage =
        CVarDef.Create("cpr.rib_break_damage", 10f, CVar.SERVERONLY);

    /// <summary>
    /// Stamina the rescuer loses per cycle. Stamina recovers about 3 per cycle, so the default tires a rescuer
    /// out after roughly three minutes of compressions.
    /// </summary>
    public static readonly CVarDef<float> CprStaminaCost =
        CVarDef.Create("cpr.stamina_cost", 5f, CVar.SERVERONLY);

    /// <summary>
    /// Chance per cycle that compressions bring a heart in the last stage of failure back to the stage before.
    /// </summary>
    public static readonly CVarDef<float> CprHeartRestartChance =
        CVarDef.Create("cpr.heart_restart_chance", 0.02f, CVar.SERVERONLY);

    /// <summary>
    /// The same chance with epinephrine in the patient's blood.
    /// </summary>
    public static readonly CVarDef<float> CprHeartRestartEpinephrineChance =
        CVarDef.Create("cpr.heart_restart_epinephrine_chance", 0.05f, CVar.SERVERONLY);
}
