using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Replicator;

/// <summary>
/// A lathe that prints replicator shells for the hive of whoever queued them.
/// </summary>
[RegisterComponent]
public sealed partial class ReplicatorFabricatorComponent : Component
{
    [DataField]
    public SoundSpecifier? FinishSound;

    [ViewVariables]
    public EntityUid? Nest;

    [ViewVariables]
    public HashSet<EntityUid> WaitingShells = new();
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
