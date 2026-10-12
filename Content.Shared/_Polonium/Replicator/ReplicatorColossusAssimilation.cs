using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Maps;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Utility;

namespace Content.Shared._Polonium.Replicator;

[RegisterComponent]
public sealed partial class ReplicatorColossusAssimilationComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Beacon;

    [DataField]
    public TimeSpan BeaconDelay = TimeSpan.FromSeconds(8);

    [DataField]
    public FixedPoint2 BeaconDamageThreshold = 40;

    [ViewVariables]
    public EntityUid? BeaconEntity;
}

/// <summary>
/// The colossus is feeding its beacon: it cannot move, shoot or use its abilities.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ReplicatorColossusLinkedComponent : Component;

/// <summary>
/// The colossus cannot shoot or use its abilities until its systems come back.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class ReplicatorColossusDisarmedComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan EndTime;
}

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ReplicatorColossusBeaconComponent : Component
{
    [DataField]
    public TimeSpan ChargeTime = TimeSpan.FromMinutes(10);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan ReadyTime;

    [DataField]
    public bool Ready;

    /// <summary>
    /// A charged beacon latches onto its colossus once it comes this close.
    /// </summary>
    [DataField]
    public float LinkRange = 6f;

    [DataField]
    public TimeSpan ReleaseDuration = TimeSpan.FromSeconds(30);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? ReleaseTime;

    [DataField]
    public bool Released;

    /// <summary>
    /// Damage taken over the release that breaks it.
    /// </summary>
    [DataField]
    public FixedPoint2 InterruptDamage = 300;

    [DataField]
    public FixedPoint2 ReleaseDamage;

    /// <summary>
    /// How much charge a broken release costs the beacon.
    /// </summary>
    [DataField]
    public TimeSpan InterruptRollback = TimeSpan.FromSeconds(150);

    /// <summary>
    /// How long the colossus stays disarmed after a broken release.
    /// </summary>
    [DataField]
    public TimeSpan InterruptDisarm = TimeSpan.FromSeconds(10);

    [DataField]
    public SpriteSpecifier? LinkBeam;

    [DataField]
    public Vector2 LinkBeamOffset;

    /// <summary>
    /// Spawned where the colossus stands, the beam ends on it.
    /// </summary>
    [DataField]
    public EntProtoId? ReleaseEffect;

    [DataField]
    public Vector2 ReleaseEffectOffset;

    [DataField]
    public SoundSpecifier? ReleaseSound;

    [DataField(required: true)]
    public EntProtoId Wave;

    /// <summary>
    /// The station is warned when this much time is left, longest first.
    /// </summary>
    [DataField]
    public List<TimeSpan> Warnings = new();

    [DataField]
    public int WarningsGiven;

    /// <summary>
    /// How many steps the countdown is shown in.
    /// </summary>
    [DataField]
    public int Stages = 5;

    [DataField]
    public int Stage;

    [DataField]
    public Color ReadyLightColor = Color.FromHex("#ffb030");

    [DataField]
    public TimeSpan OpenTime = TimeSpan.FromSeconds(0.8);

    [DataField]
    public SoundSpecifier? DeploySound;

    [DataField]
    public SoundSpecifier? StageSound;

    /// <summary>
    /// How much higher the stage sound plays with every stage.
    /// </summary>
    [DataField]
    public float StagePitchStep = 0.05f;

    [DataField]
    public SoundSpecifier? ReadySound;

    /// <summary>
    /// Replaces the ambient loop once the beacon is ready.
    /// </summary>
    [DataField]
    public SoundSpecifier? ReadyAmbience;

    /// <summary>
    /// Starts for everyone in the round when <see cref="MusicLead"/> is left on the countdown.
    /// </summary>
    [DataField]
    public SoundSpecifier? Music;

    [DataField]
    public TimeSpan MusicLead;

    [DataField]
    public bool MusicStarted;

    [ViewVariables]
    public ResolvedSoundSpecifier? ResolvedMusic;

    [ViewVariables]
    public EntityUid? Colossus;

    [ViewVariables]
    public EntityUid? ReleaseEffectEntity;

    [ViewVariables]
    public EntityUid? ReleaseSoundEntity;

    [ViewVariables]
    public Color? ChargingLightColor;

    [ViewVariables]
    public SoundSpecifier? ChargingAmbience;

    [ViewVariables]
    public bool? Open;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ReplicatorAssimilationWaveComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan StartTime;

    /// <summary>
    /// Tiles per second.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Speed = 9f;

    [DataField, AutoNetworkedField]
    public float MaxRadius;

    [DataField(required: true)]
    public EntProtoId Wall;

    [DataField(required: true)]
    public EntProtoId ReinforcedWall;

    [DataField]
    public float ReinforcedThreshold = 300;

    [DataField(required: true)]
    public ProtoId<ContentTileDefinition> Tile;

    [DataField]
    public EntProtoId? ConvertEffect;

    [DataField]
    public float ConvertEffectChance = 0.3f;

    [DataField]
    public SoundSpecifier? ConvertSound;

    [DataField]
    public TimeSpan ConvertSoundInterval = TimeSpan.FromSeconds(0.15);

    /// <summary>
    /// What every humanoid the wave reaches is rebuilt into.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Replicator;

    /// <summary>
    /// What every other creature the wave reaches is turned into.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Remains;

    [DataField]
    public SoundSpecifier? Sound;

    [ViewVariables]
    public EntityUid Grid;

    [ViewVariables]
    public EntityUid? Nest;

    [ViewVariables]
    public List<(float Distance, Vector2i Indices)> Tiles = new();

    [ViewVariables]
    public int NextTile;

    [ViewVariables]
    public TimeSpan NextConvertSound;
}

[Serializable, NetSerializable]
public enum ReplicatorColossusBeaconVisuals : byte
{
    Stage,
    State,
}

[Serializable, NetSerializable]
public enum ReplicatorColossusBeaconState : byte
{
    Charging,
    Ready,
    Firing,
}

public enum ReplicatorColossusBeaconVisualLayers : byte
{
    Orb,
    Claws,
    Lights,
}

public sealed partial class ReplicatorColossusBeaconActionEvent : WorldTargetActionEvent;

[Serializable, NetSerializable]
public sealed partial class ReplicatorColossusBeaconDoAfterEvent : DoAfterEvent
{
    [DataField]
    public NetCoordinates Location;

    private ReplicatorColossusBeaconDoAfterEvent()
    {
    }

    public ReplicatorColossusBeaconDoAfterEvent(NetCoordinates location)
    {
        Location = location;
    }

    public override DoAfterEvent Clone() => this;
}
