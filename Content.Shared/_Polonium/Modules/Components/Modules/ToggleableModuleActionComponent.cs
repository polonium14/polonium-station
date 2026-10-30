using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Modules.Components.Modules;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ToggleableModuleActionComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId ActionId = "ActionModuleToggle";

    [ViewVariables, AutoNetworkedField]
    public EntityUid? Action;
}
