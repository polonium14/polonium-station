using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Polonium.Replicator;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ReplicatorFabricatorComponent : Component
{
    [DataField]
    public int ShellCost = 200;

    [DataField]
    public TimeSpan ProductionTime = TimeSpan.FromSeconds(30);

    [DataField]
    public EntProtoId Shell = "SpawnPointGhostReplicator";

    [DataField]
    public SoundSpecifier? FinishSound;

    [ViewVariables]
    public EntityUid? Nest;

    [ViewVariables]
    public EntityUid? WaitingShell;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? FinishTime;
}

[Serializable, NetSerializable]
public enum ReplicatorFabricatorVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum ReplicatorFabricatorState : byte
{
    Idle,
    Working,
    Ready,
}
