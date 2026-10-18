using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Replicator;

[RegisterComponent]
public sealed partial class ReplicatorColossusFoundryComponent : Component
{
    [DataField(required: true)]
    public List<EntProtoId> Parts = new();

    [DataField]
    public int Stage;

    [DataField]
    public TimeSpan InsertTime = TimeSpan.FromSeconds(4);

    [DataField]
    public int RequiredNestLevel = 3;

    [DataField]
    public int RoomRadius = 2;

    [DataField]
    public int FootprintRadius = 1;

    [DataField]
    public EntProtoId Spawner = "SpawnPointGhostReplicatorColossus";

    [DataField]
    public LocId WarningAnnouncement = "replicator-colossus-foundry-warning";

    [DataField]
    public SoundSpecifier? InsertSound;

    [DataField]
    public SoundSpecifier? CompleteSound;

    [DataField]
    public SoundSpecifier? InitializeSound;

    [ViewVariables]
    public EntityUid? Nest;

    [ViewVariables]
    public EntityUid? WaitingSpawner;
}

[Serializable, NetSerializable]
public enum ReplicatorColossusFoundryVisuals : byte
{
    Stage,
    State,
}

[Serializable, NetSerializable]
public enum ReplicatorColossusFoundryState : byte
{
    Assembling,
    Complete,
    Awaiting,
}

[Serializable, NetSerializable]
public enum ReplicatorColossusFoundryVisualLayers : byte
{
    Stage,
    Status,
}

[Serializable, NetSerializable]
public sealed partial class ReplicatorColossusInsertPartDoAfterEvent : SimpleDoAfterEvent;
