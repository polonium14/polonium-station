using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Modules.Components.Modules;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ToggleableModuleComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public bool Toggled;

    [DataField]
    public TimeSpan MessageDelay = TimeSpan.FromSeconds(0.25);

    [ViewVariables, AutoNetworkedField, AutoPausedField]
    public TimeSpan NextMessage;
}
