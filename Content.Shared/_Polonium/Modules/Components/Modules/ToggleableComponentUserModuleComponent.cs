using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Modules.Components.Modules;

[RegisterComponent, NetworkedComponent]
public sealed partial class ToggleableComponentUserModuleComponent : Component
{
    [DataField]
    public ComponentRegistry Components;
}
