using Robust.Shared.GameStates;

namespace Content.Shared._Shitmed.Medical.Surgery;

[RegisterComponent, NetworkedComponent]
public sealed partial class OperatingTableComponent : Component
{
    [DataField]
    public float SpeedModifier = 1f;
}
