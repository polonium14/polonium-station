using Content.Shared.AlertLevel;
using Content.Shared.Explosion;
using Content.Shared.StatusIcon;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Polonium.Replicator;

[RegisterComponent, NetworkedComponent]
public sealed partial class ReplicatorColossusComponent : Component
{
    [DataField]
    public TimeSpan LaunchDuration = TimeSpan.FromSeconds(16.5);

    [DataField]
    public ProtoId<FactionIconPrototype> Icon = "ReplicatorHiveColossus";

    [DataField]
    public SoundSpecifier? LaunchSound;

    [DataField]
    public bool LaunchSoundPlayed;

    [DataField]
    public EntProtoId? LaunchEffect;

    [DataField]
    public EntProtoId? LaunchLight;

    [DataField]
    public float ClearRadius = 2.5f;

    [DataField]
    public float LaunchThrowDistance = 3f;

    [DataField]
    public TimeSpan LaunchKnockdown = TimeSpan.FromSeconds(2);

    [DataField]
    public ProtoId<AlertLevelPrototype> AlertLevel = "DeltaColossus";

    [DataField]
    public ProtoId<AlertLevelPrototype> DefeatAlertLevel = "Red";

    [DataField]
    public LocId Announcement = "replicator-colossus-announcement";

    [DataField]
    public LocId DefeatAnnouncement = "replicator-colossus-defeat-announcement";

    [DataField]
    public EntProtoId Wreck = "ReplicatorColossusWreck";

    [DataField]
    public ProtoId<ExplosionPrototype> Explosion = "ReplicatorColossus";

    [DataField]
    public float ExplosionIntensity = 150f;

    [DataField]
    public float ExplosionSlope = 3f;

    [DataField]
    public float ExplosionMaxTileIntensity = 5f;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ReplicatorColossusLaunchComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan StartTime;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan EndTime;
}

[Serializable, NetSerializable]
public enum ReplicatorColossusVisualLayers : byte
{
    Body,
    Thrust,
    Launch,
    LaunchGlow,
}
