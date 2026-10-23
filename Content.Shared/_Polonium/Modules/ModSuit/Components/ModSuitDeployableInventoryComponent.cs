using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Modules.ModSuit.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class ModSuitDeployableInventoryComponent : Component
{
    [DataField]
    public string ContainerId = "mod_suit_inventory";

    [ViewVariables]
    public ContainerSlot StoredItem;
}
