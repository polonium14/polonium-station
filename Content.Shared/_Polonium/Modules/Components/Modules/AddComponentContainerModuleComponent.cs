using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Modules.Components.Modules;

[RegisterComponent, NetworkedComponent]
public sealed partial class AddComponentContainerModuleComponent : Component
{
    [DataField]
    public ComponentRegistry Components;
}
