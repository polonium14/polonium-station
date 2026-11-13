using Robust.Shared.Configuration;

namespace Content.Shared._Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    /// <summary>
    /// Whether the client rolls off the light map where lights overlap, instead of washing sprites out.
    /// </summary>
    public static readonly CVarDef<bool> LightTonemap =
        CVarDef.Create("lighting.tonemap", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// The light map is multiplied by this before the roll-off.
    /// </summary>
    public static readonly CVarDef<float> LightTonemapExposure =
        CVarDef.Create("lighting.tonemap_exposure", 1f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Light level the roll-off starts at, everything below it is left as is.
    /// Has to be below <see cref="LightTonemapMax"/>, the client lowers it to just under the maximum otherwise.
    /// </summary>
    public static readonly CVarDef<float> LightTonemapKnee =
        CVarDef.Create("lighting.tonemap_knee", 0.7f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Light level the roll-off approaches but never reaches, however many lights overlap.
    /// Has to be above <see cref="LightTonemapKnee"/>.
    /// </summary>
    public static readonly CVarDef<float> LightTonemapMax =
        CVarDef.Create("lighting.tonemap_max", 1.15f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Whether screens, lamps and other glowing things bleed light around them. Costs a few extra passes per frame.
    /// </summary>
    public static readonly CVarDef<bool> Bloom =
        CVarDef.Create("lighting.bloom", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// How bright the bloom is, from 0 to 3. The blur spreads the glow thin, so values above 1 are useful.
    /// </summary>
    public static readonly CVarDef<float> BloomStrength =
        CVarDef.Create("lighting.bloom_strength", 1f, CVar.CLIENTONLY | CVar.ARCHIVE); // Polonium: our lamps are brighter than Starlight's, 0.7 was barely visible

    /// <summary>
    /// How much a pixel has to outshine its light before it glows, from 0 to 1.
    /// </summary>
    public static readonly CVarDef<float> BloomThreshold =
        CVarDef.Create("lighting.bloom_threshold", 0.1f, CVar.CLIENTONLY | CVar.ARCHIVE); // Polonium: lower, so things under our brighter lamps still glow

    /// <summary>
    /// How far the bloom spreads. Same units as the engine's light blur.
    /// </summary>
    public static readonly CVarDef<float> BloomRadius =
        CVarDef.Create("lighting.bloom_radius", 10f, CVar.CLIENTONLY | CVar.ARCHIVE);
}
