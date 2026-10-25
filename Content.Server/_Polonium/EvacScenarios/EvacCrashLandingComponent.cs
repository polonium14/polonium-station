using Content.Shared.Explosion;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Polonium.EvacScenarios;

[RegisterComponent, AutoGenerateComponentPause, Access(typeof(EvacCrashLandingSystem))]
public sealed partial class EvacCrashLandingComponent : Component
{
    /// <summary>
    /// Moment of the scenario when the shuttles hit the ground.
    /// </summary>
    [DataField]
    public TimeSpan CrashTime = TimeSpan.FromSeconds(190);

    /// <summary>
    /// Planets to crash on, one is picked at random.
    /// </summary>
    [DataField(required: true)]
    public List<EvacCrashPlanet> Planets = new();

    /// <summary>
    /// Distance between the landing sites when several shuttles crash on the same planet.
    /// </summary>
    [DataField]
    public float LandingSpacing = 200f;

    [DataField]
    public SoundSpecifier? ImpactSound = new SoundCollectionSpecifier("ShuttleImpactSound");

    [DataField]
    public float ImpactShake = 3f;

    [DataField]
    public int ImpactExplosions = 3;

    [DataField]
    public ProtoId<ExplosionPrototype> ImpactExplosionType = "Default";

    [DataField]
    public float ImpactExplosionIntensity = 20f;

    [DataField]
    public float ImpactExplosionMaxTileIntensity = 5f;

    [DataField]
    public List<EvacCrashMobWave> MobWaves = new();

    /// <summary>
    /// How far from the hull the monsters show up.
    /// </summary>
    [DataField]
    public float MobMinDistance = 3f;

    /// <inheritdoc cref="MobMinDistance"/>
    [DataField]
    public float MobMaxDistance = 9f;

    [DataField]
    public EntityUid? Planet;

    [DataField]
    public int PlanetIndex;

    /// <summary>
    /// Shuttles sent to the planet.
    /// </summary>
    [DataField]
    public HashSet<EntityUid> Diverted = new();

    [DataField]
    public HashSet<EntityUid> Landed = new();

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? LandedAt;

    [DataField]
    public int NextMobWave;
}

[DataDefinition]
public sealed partial class EvacCrashPlanet
{
    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public ProtoId<BiomeTemplatePrototype> Biome;

    [DataField]
    public Color? Light;

    /// <summary>
    /// Monsters living on the planet, with their weights.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<EntProtoId, float> Mobs = new();
}

[DataDefinition]
public sealed partial class EvacCrashMobWave
{
    /// <summary>
    /// Time after the crash at which the wave shows up.
    /// </summary>
    [DataField(required: true)]
    public TimeSpan Delay;

    /// <summary>
    /// Monsters per living player aboard the shuttle.
    /// </summary>
    [DataField]
    public float PerPassenger = 0.25f;

    [DataField]
    public int Min = 2;

    [DataField]
    public int Max = 10;
}
