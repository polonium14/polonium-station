using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// How many times longer than its base time a surgery step can take before it starts to fail.
    /// A proper tool on a patient lying on the floor is 1.
    /// </summary>
    public static readonly CVarDef<float> SurgeryBotchThreshold =
        CVarDef.Create("surgery.botch_threshold", 1.25f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Chance to botch a step for every second of its base time, per unit of slowdown above the threshold.
    /// </summary>
    public static readonly CVarDef<float> SurgeryBotchChancePerSecond =
        CVarDef.Create("surgery.botch_chance_per_second", 0.2f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Slash damage the operated part takes from a botched step.
    /// </summary>
    public static readonly CVarDef<float> SurgeryBotchDamage =
        CVarDef.Create("surgery.botch_damage", 5f, CVar.SERVERONLY);

    /// <summary>
    /// Surgery speed multiplier on a patient who feels nothing: anesthetized or on a painkiller.
    /// </summary>
    public static readonly CVarDef<float> SurgeryNumbPatientSpeed =
        CVarDef.Create("surgery.numb_patient_speed", 1.25f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Chance that a surgeon with shaking hands botches a step, whatever the tool.
    /// </summary>
    public static readonly CVarDef<float> SurgeryJitterBotchChance =
        CVarDef.Create("surgery.jitter_botch_chance", 0.25f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Blood an open incision in the torso loses every 3 seconds until its bleeders are clamped.
    /// The head bleeds half of it, an arm or a leg a quarter.
    /// </summary>
    public static readonly CVarDef<float> SurgeryOpenIncisionBleed =
        CVarDef.Create("surgery.open_incision_bleed", 2.5f, CVar.SERVERONLY);

    /// <summary>
    /// Blood an open incision in the torso loses every 3 seconds once its bleeders are clamped.
    /// </summary>
    public static readonly CVarDef<float> SurgeryClampedIncisionBleed =
        CVarDef.Create("surgery.clamped_incision_bleed", 0.3f, CVar.SERVERONLY);

    /// <summary>
    /// The most a slow tool and poor conditions can add up to, so an improvised tool is never outright useless.
    /// A clumsy surgeon's own chance comes on top.
    /// </summary>
    public static readonly CVarDef<float> SurgeryBotchMaxChance =
        CVarDef.Create("surgery.botch_max_chance", 0.9f, CVar.SERVER | CVar.REPLICATED);
}
