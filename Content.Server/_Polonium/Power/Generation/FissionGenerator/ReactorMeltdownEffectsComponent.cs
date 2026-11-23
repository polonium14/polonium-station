using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Polonium.Power.Generation.FissionGenerator;

/// <summary>
/// Station-wide tremors and flickering lights while a nuclear reactor heads for a meltdown,
/// and the blast, shockwave, smoke and corium when it melts down.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause, Access(typeof(ReactorMeltdownEffectsSystem))]
public sealed partial class ReactorMeltdownEffectsComponent : Component
{
    /// <summary>
    /// How long the reactor stays above its meltdown temperature before it actually melts down.
    /// Cooling it down in this time still saves it.
    /// </summary>
    [DataField]
    public TimeSpan CriticalDuration = TimeSpan.FromSeconds(45);

    /// <summary>
    /// Constant trembling of the screen during the build-up, next to the reactor right before the meltdown.
    /// </summary>
    [DataField]
    public float RumbleShake = 0.35f;

    /// <summary>
    /// Earthquake rumble during the build-up, heard by everyone who feels the tremors
    /// and louder closer to the reactor and closer to the meltdown.
    /// </summary>
    [DataField]
    public SoundSpecifier? RumbleSound = new SoundPathSpecifier("/Audio/_Polonium/Machines/Reactor/reactor_rumble.ogg");

    [ViewVariables]
    public Dictionary<ICommonSession, EntityUid> RumbleStreams = new();

    /// <summary>
    /// Range in which tremors are felt at full strength. Players further away on the station
    /// still feel <see cref="DistantShakeFraction"/> of it.
    /// </summary>
    [DataField]
    public float ShakeRange = 40f;

    [DataField]
    public float DistantShakeFraction = 0.3f;

    [DataField]
    public TimeSpan MaxTremorInterval = TimeSpan.FromSeconds(20);

    [DataField]
    public TimeSpan MinTremorInterval = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan TremorDuration = TimeSpan.FromSeconds(1.5);

    [DataField]
    public float TremorShake = 0.7f;

    /// <summary>
    /// Chance of each light on the station to flicker during a tremor right before the meltdown.
    /// </summary>
    [DataField]
    public float TremorFlickerChance = 0.2f;

    [DataField]
    public TimeSpan TremorFlickerDuration = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan MeltdownTremorDuration = TimeSpan.FromSeconds(3);

    [DataField]
    public float MeltdownShake = 1f;

    [DataField]
    public float MeltdownFlickerChance = 0.7f;

    [DataField]
    public TimeSpan MeltdownFlickerDuration = TimeSpan.FromSeconds(8);

    [DataField]
    public float BulbBurstRange = 9f;

    [DataField]
    public float BulbBurstChance = 0.5f;

    [DataField]
    public float ShockwaveRadius = 30f;

    [DataField]
    public float ShockwaveDuration = 1.8f;

    [DataField]
    public float ShockwaveIntensity = 0.9f;

    [DataField]
    public TimeSpan MinPulseInterval = TimeSpan.FromSeconds(25);

    [DataField]
    public TimeSpan MaxPulseInterval = TimeSpan.FromSeconds(60);

    [DataField]
    public float PulseRadius = 6f;

    [DataField]
    public float PulseDuration = 1.2f;

    [DataField]
    public float PulseIntensity = 0.35f;

    /// <summary>
    /// Rumble of each tremor, heard by everyone who feels it and louder closer to the reactor.
    /// </summary>
    [DataField]
    public SoundSpecifier? TremorSound = new SoundCollectionSpecifier("ReactorTremor");

    /// <summary>
    /// Groan of the reactor vessel heard around the reactor with each tremor of the build-up.
    /// </summary>
    [DataField]
    public SoundSpecifier? CreakSound = new SoundCollectionSpecifier("ReactorCreak");

    /// <summary>
    /// Blast of the meltdown, heard by everyone who feels the tremors and louder closer to the reactor.
    /// </summary>
    [DataField]
    public SoundSpecifier? ExplosionSound = new SoundPathSpecifier("/Audio/_Polonium/Machines/Reactor/reactor_explosion.ogg");

    [DataField]
    public SoundSpecifier? PulseSound = new SoundCollectionSpecifier("ReactorPulse");

    /// <summary>
    /// Volume added to the tremors and the blast heard across the station.
    /// </summary>
    [DataField]
    public float StationSoundVolume;

    [DataField]
    public EntProtoId SmokeColumnPrototype = "ReactorSmokeColumn";

    [DataField]
    public EntProtoId CoriumPrototype = "Corium";

    /// <summary>
    /// Corium tiles that flow out of the reactor at once, after <see cref="FirstCoriumDelay"/>.
    /// </summary>
    [DataField]
    public int InitialCorium = 6;

    [DataField]
    public int MaxCorium = 20;

    [DataField]
    public TimeSpan FirstCoriumDelay = TimeSpan.FromSeconds(8);

    [DataField]
    public TimeSpan MinCoriumInterval = TimeSpan.FromSeconds(8);

    [DataField]
    public TimeSpan MaxCoriumInterval = TimeSpan.FromSeconds(16);

    [DataField]
    public int CoriumSpawned;

    [DataField]
    public List<EntityUid> Corium = new();

    /// <summary>
    /// When the reactor went over its meltdown temperature, if it still is.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? CriticalStart;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextUpdate;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextRumbleShake;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextTremor;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextPulse;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextCorium;

    [ViewVariables]
    public bool BuildingUp;

    [ViewVariables]
    public List<EntityUid> StationLights = new();
}
