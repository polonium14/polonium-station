using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Utility;

namespace Content.Shared._Polonium.Replicator;

public sealed partial class ReplicatorDashActionEvent : InstantActionEvent
{
    [DataField]
    public float Speed = 15f;

    [DataField]
    public SoundSpecifier? Sound;
}

public sealed partial class ReplicatorVolleyActionEvent : WorldTargetActionEvent
{
    [DataField]
    public EntProtoId Projectile = "BulletReplicatorDisabler";

    [DataField]
    public int Count = 3;

    [DataField]
    public Angle Spread = Angle.FromDegrees(24);

    [DataField]
    public float Speed = 20f;

    [DataField]
    public SoundSpecifier? Sound;
}

public sealed partial class ReplicatorRepairActionEvent : EntityTargetActionEvent
{
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(6);

    [DataField]
    public float Heal = 25f;

    [DataField]
    public float Range = 2f;

    [DataField]
    public EntProtoId? Effect;

    [DataField]
    public SpriteSpecifier? Beam;

    [DataField]
    public SoundSpecifier? Sound;
}

public sealed partial class ReplicatorSendCargoActionEvent : InstantActionEvent
{
    [DataField]
    public EntProtoId? Effect;

    [DataField]
    public SoundSpecifier? Sound;
}

public sealed partial class ReplicatorMarkActionEvent : EntityTargetActionEvent
{
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(45);

    [DataField]
    public SoundSpecifier? Sound;
}

public sealed partial class ReplicatorScanActionEvent : InstantActionEvent
{
    [DataField]
    public float Range = 12f;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(20);

    [DataField]
    public EntProtoId? Effect;

    [DataField]
    public SoundSpecifier? Sound;
}

[Serializable, NetSerializable]
public sealed partial class ReplicatorRepairDoAfterEvent : DoAfterEvent
{
    [DataField]
    public float Heal;

    private ReplicatorRepairDoAfterEvent()
    {
    }

    public ReplicatorRepairDoAfterEvent(float heal)
    {
        Heal = heal;
    }

    public override DoAfterEvent Clone() => this;
}

[RegisterComponent]
public sealed partial class ReplicatorRepairingComponent : Component
{
    [ViewVariables]
    public EntityUid? Effect;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ReplicatorMarkedComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan EndTime;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ReplicatorTrackerComponent : Component
{
    public override bool SendOnlyToOwner => true;

    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(1);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextUpdate;

    [ViewVariables, AutoNetworkedField]
    public MapCoordinates Target = MapCoordinates.Nullspace;

    [DataField]
    public float MinDistance = 6f;
}

/// <summary>
/// Makes a cloaked replicator show itself while it sprints. Walking and drifting do not count.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ReplicatorSprintRevealComponent : Component
{
    /// <summary>
    /// Visibility gained per tile covered at a sprint.
    /// </summary>
    [DataField]
    public float VisibilityRate = 0.6f;
}
