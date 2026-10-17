using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Polonium.EvacScenarios;

/// <summary>
/// Throws meteors at the scenario's shuttles while they fly through hyperspace.
/// The shower goes through phases that get heavier as the flight goes on.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause, Access(typeof(EvacMeteorShowerSystem))]
public sealed partial class EvacMeteorShowerComponent : Component
{
    /// <summary>
    /// Phases of the shower, each lasting until the next one begins. Nothing is thrown before the first one.
    /// </summary>
    [DataField(required: true)]
    public List<EvacMeteorShowerPhase> Phases = new();

    /// <summary>
    /// Moment of the scenario after which no more meteors are thrown. The shower also stops once a shuttle leaves hyperspace.
    /// </summary>
    [DataField]
    public TimeSpan? StopTime;

    /// <summary>
    /// Speed of the meteors relative to the shuttle.
    /// </summary>
    [DataField]
    public float Speed = 12f;

    /// <summary>
    /// How far beyond the hull the meteors appear.
    /// </summary>
    [DataField]
    public float MinDistance = 10f;

    /// <inheritdoc cref="MinDistance"/>
    [DataField]
    public float MaxDistance = 18f;

    /// <summary>
    /// Meteors come from ahead of the shuttle, at most this far off its course.
    /// </summary>
    [DataField]
    public Angle Spread = Angle.FromDegrees(70);

    /// <summary>
    /// Meteors explode as soon as they hit the shuttle instead of once the hull wears them down.
    /// </summary>
    [DataField]
    public bool ExplodeOnImpact = true;

    [DataField]
    public float ImpactIntensityMultiplier = 1f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? NextWave;

    [DataField]
    public HashSet<EntityUid> Meteors = new();
}

[DataDefinition]
public sealed partial class EvacMeteorShowerPhase
{
    /// <summary>
    /// Moment of the scenario the phase begins at.
    /// </summary>
    [DataField(required: true)]
    public TimeSpan Start;

    [DataField(required: true)]
    public Dictionary<EntProtoId, float> Meteors = new();

    /// <summary>
    /// How many meteors each shuttle gets per wave.
    /// </summary>
    [DataField]
    public int MinPerWave = 1;

    /// <inheritdoc cref="MinPerWave"/>
    [DataField]
    public int MaxPerWave = 1;

    [DataField]
    public TimeSpan MinInterval = TimeSpan.FromSeconds(5);

    /// <inheritdoc cref="MinInterval"/>
    [DataField]
    public TimeSpan MaxInterval = TimeSpan.FromSeconds(8);
}
