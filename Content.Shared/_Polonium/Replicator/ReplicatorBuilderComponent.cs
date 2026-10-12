using Content.Shared.Maps;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Replicator;

[RegisterComponent, NetworkedComponent]
public sealed partial class ReplicatorBuilderComponent : Component
{
    [DataField]
    public float BuildTimeMultiplier = 1f;

    /// <summary>
    /// The hive builds only on this floor.
    /// </summary>
    [DataField]
    public ProtoId<ContentTileDefinition> Floor = "FloorReplicator";

    [ViewVariables]
    public ProtoId<ReplicatorStructurePrototype>? Selected;
}
