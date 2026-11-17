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

    /// <summary>
    /// Poison damage per second from a dead or missing liver, and again from dead or missing kidneys.
    /// </summary>
    public static readonly CVarDef<float> OrgansDeadOrganPoison =
        CVarDef.Create("organs.dead_organ_poison", 1f, CVar.SERVERONLY);

    /// <summary>
    /// Chance that poison damage also hurts the kidneys, and separately the liver.
    /// </summary>
    public static readonly CVarDef<float> OrgansToxinOrganDamageChance =
        CVarDef.Create("organs.toxin_organ_damage_chance", 0.3f, CVar.SERVERONLY);

    /// <summary>
    /// Chance per second that a sick organ falls into failure, per percent of its integrity it has lost.
    /// </summary>
    public static readonly CVarDef<float> OrgansFailureContractChance =
        CVarDef.Create("organs.failure_contract_chance", 0.0005f, CVar.SERVERONLY);

    /// <summary>
    /// Chance per second that a failure moves on to its next stage.
    /// </summary>
    public static readonly CVarDef<float> OrgansFailureStageChance =
        CVarDef.Create("organs.failure_stage_chance", 0.0025f, CVar.SERVERONLY);

    /// <summary>
    /// The most a lobotomized brain counts for in the senses, 0 to 1, whatever its integrity.
    /// </summary>
    public static readonly CVarDef<float> OrgansLobotomyBrainEfficiency =
        CVarDef.Create("organs.lobotomy_brain_efficiency", 0.2f, CVar.SERVERONLY);

    /// <summary>
    /// Seconds a healthy organ lasts outside a body and outside a freezer before it dies. Zero turns the decay off.
    /// </summary>
    public static readonly CVarDef<float> OrgansDecayTime =
        CVarDef.Create("organs.decay_time", 900f, CVar.SERVERONLY);

    /// <summary>
    /// Percentage points the oxygen saturation reads lower for a while after smoking.
    /// </summary>
    public static readonly CVarDef<float> OrgansSmokingSpO2Drop =
        CVarDef.Create("organs.smoking_spo2_drop", 3f, CVar.SERVERONLY);

    /// <summary>
    /// A body whose saturation reads below this coughs now and then.
    /// </summary>
    public static readonly CVarDef<float> OrgansCoughSpO2 =
        CVarDef.Create("organs.cough_spo2", 97f, CVar.SERVERONLY);

    /// <summary>
    /// A body whose saturation reads below this gasps instead of coughing.
    /// </summary>
    public static readonly CVarDef<float> OrgansGaspSpO2 =
        CVarDef.Create("organs.gasp_spo2", 92f, CVar.SERVERONLY);

    /// <summary>
    /// Chance every 5 seconds that a body with a low saturation reading coughs or gasps.
    /// </summary>
    public static readonly CVarDef<float> OrgansCoughChance =
        CVarDef.Create("organs.cough_chance", 0.3f, CVar.SERVERONLY);

    /// <summary>
    /// Share of a hit's damage that an organ loses in integrity when the hit injures it.
    /// </summary>
    public static readonly CVarDef<float> OrgansTraumaDamageMultiplier =
        CVarDef.Create("organs.trauma_damage_multiplier", 0.25f, CVar.SERVERONLY);
}
