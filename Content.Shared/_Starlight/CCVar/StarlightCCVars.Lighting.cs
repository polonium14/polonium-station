using Robust.Shared.Configuration;

namespace Content.Shared._Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    /// <summary>
    /// Whether screens, lamps and other glowing things bleed light around them. Costs a few extra passes per frame.
    /// </summary>
    public static readonly CVarDef<bool> Bloom =
        CVarDef.Create("lighting.bloom", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// How bright the bloom is, from 0 to 3. The blur spreads the glow thin, so values above 1 are useful.
    /// </summary>
    public static readonly CVarDef<float> BloomStrength =
        CVarDef.Create("lighting.bloom_strength", 0.7f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// How much a pixel has to outshine its light before it glows, from 0 to 1.
    /// </summary>
    public static readonly CVarDef<float> BloomThreshold =
        CVarDef.Create("lighting.bloom_threshold", 0.15f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// How far the bloom spreads. Same units as the engine's light blur.
    /// </summary>
    public static readonly CVarDef<float> BloomRadius =
        CVarDef.Create("lighting.bloom_radius", 10f, CVar.CLIENTONLY | CVar.ARCHIVE);
}
