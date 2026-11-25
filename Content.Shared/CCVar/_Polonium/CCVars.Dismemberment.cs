using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Multiplier on the chance that a cut severs a badly hurt limb.
    /// </summary>
    public static readonly CVarDef<float> DismembermentChanceMultiplier =
        CVarDef.Create("dismemberment.chance_multiplier", 0.5f, CVar.SERVERONLY);

    /// <summary>
    /// Blunt and piercing damage one explosion has to deal to a body before it can tear a limb off.
    /// </summary>
    public static readonly CVarDef<float> DismembermentExplosionThreshold =
        CVarDef.Create("dismemberment.explosion_threshold", 40f, CVar.SERVERONLY);

    /// <summary>
    /// Chance that an explosion tears one arm or leg off, per point of blunt and piercing damage above the threshold.
    /// </summary>
    public static readonly CVarDef<float> DismembermentExplosionChancePerDamage =
        CVarDef.Create("dismemberment.explosion_chance_per_damage", 0.005f, CVar.SERVERONLY);

    /// <summary>
    /// Share of a severed limb's damage that the part it hung on takes when the limb is torn off.
    /// </summary>
    public static readonly CVarDef<float> DismembermentParentDamageShare =
        CVarDef.Create("dismemberment.parent_damage_share", 0.5f, CVar.SERVERONLY);

    /// <summary>
    /// The least damage the part a limb hung on takes when the limb is torn off.
    /// </summary>
    public static readonly CVarDef<float> DismembermentParentDamageMin =
        CVarDef.Create("dismemberment.parent_damage_min", 15f, CVar.SERVERONLY);

    /// <summary>
    /// The most damage the part a limb hung on takes when the limb is torn off.
    /// </summary>
    public static readonly CVarDef<float> DismembermentParentDamageMax =
        CVarDef.Create("dismemberment.parent_damage_max", 50f, CVar.SERVERONLY);

    /// <summary>
    /// Bleeding added to the body when a limb is torn off. It clots like any other bleeding.
    /// </summary>
    public static readonly CVarDef<float> DismembermentBleed =
        CVarDef.Create("dismemberment.bleed", 5f, CVar.SERVERONLY);
}
