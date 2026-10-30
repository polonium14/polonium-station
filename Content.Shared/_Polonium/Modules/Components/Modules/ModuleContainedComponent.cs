using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Modules.Components.Modules;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ModuleContainedComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Container;
}
