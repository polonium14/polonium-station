using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Modules.Components.Modules;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ToggleableUiModuleComponent : Component
{
    [ViewVariables, AutoNetworkedField, AutoPausedField]
    public TimeSpan NextButtonPress;

    [DataField]
    public TimeSpan ButtonDelay = TimeSpan.FromSeconds(0.5);
}
