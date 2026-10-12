using Content.Shared._Polonium.Replicator;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Replicator;

/// <summary>
/// Gives a spawned replicator a class and grown modules without an evolution chamber.
/// </summary>
[RegisterComponent]
public sealed partial class ReplicatorPresetComponent : Component
{
    [DataField]
    public ProtoId<ReplicatorClassPrototype>? Class;

    [DataField]
    public Dictionary<ProtoId<ReplicatorModulePrototype>, int> Stages = new();

    /// <summary>
    /// Links the replicator to the nearest nest on its map, if it has none.
    /// </summary>
    [DataField]
    public bool JoinNest = true;
}
