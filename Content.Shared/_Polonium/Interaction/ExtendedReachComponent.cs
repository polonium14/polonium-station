using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Interaction;

[RegisterComponent, NetworkedComponent]
public sealed partial class ExtendedReachComponent : Component
{
    [DataField]
    public float Bonus = 1f;
}
