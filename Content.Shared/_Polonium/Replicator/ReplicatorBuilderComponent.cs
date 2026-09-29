using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Replicator;

[RegisterComponent, NetworkedComponent]
public sealed partial class ReplicatorBuilderComponent : Component
{
    [DataField]
    public float BuildTimeMultiplier = 1f;
}
