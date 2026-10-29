using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Number of burnt decals required on a tile before a reagent fire can deconstruct it.
    /// </summary>
    public static readonly CVarDef<int> TileDestroyBurntDecals =
        CVarDef.Create("reagent_fire.tile_destroy_burnt_decals", 4, CVar.SERVERONLY);

    /// <summary>
    /// Minimum effective flammability required for a reagent fire to deconstruct a tile.
    /// </summary>
    public static readonly CVarDef<float> TileDestroyEffectiveFlammability =
        CVarDef.Create("reagent_fire.tile_destroy_effective_flammability", 4f, CVar.SERVERONLY);
}
