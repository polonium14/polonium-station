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
}
