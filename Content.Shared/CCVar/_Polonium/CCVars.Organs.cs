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
}
