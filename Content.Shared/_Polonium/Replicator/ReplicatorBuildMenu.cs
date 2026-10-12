using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Replicator;

[RegisterComponent]
public sealed partial class ReplicatorBuildMenuComponent : Component;

[RegisterComponent]
public sealed partial class ReplicatorHiveStructureComponent : Component
{
    [DataField]
    public ProtoId<ReplicatorStructurePrototype>? Structure;
}

[Serializable, NetSerializable]
public enum ReplicatorBuildUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class ReplicatorBuildMenuState(List<ReplicatorBuildMenuEntry> entries) : BoundUserInterfaceState
{
    public readonly List<ReplicatorBuildMenuEntry> Entries = entries;
}

[Serializable, NetSerializable]
public readonly record struct ReplicatorBuildMenuEntry(ProtoId<ReplicatorStructurePrototype> Structure, int Cost, bool Unlocked);

[Serializable, NetSerializable]
public sealed class ReplicatorBuildSelectMessage(ProtoId<ReplicatorStructurePrototype> structure) : BoundUserInterfaceMessage
{
    public readonly ProtoId<ReplicatorStructurePrototype> Structure = structure;
}
