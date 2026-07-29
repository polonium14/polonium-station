using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.Construction.FloorResin;

[RegisterComponent, NetworkedComponent]
public sealed partial class XenoStickyResinComponent : Component
{
    [DataField]
    public float SpeedMultiplier = 0.4f;
}
