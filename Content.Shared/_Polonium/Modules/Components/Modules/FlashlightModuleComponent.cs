using Content.Shared._Polonium.Modules.Modules;
using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Modules.Components.Modules;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class FlashlightModuleComponent : Component
{
    [DataField, AutoNetworkedField, Access(typeof(SharedFlashlightModuleSystem))]
    public Color Color = Color.White;

    [DataField]
    public TimeSpan UpdateRate = TimeSpan.FromSeconds(0.02);

    [ViewVariables, AutoNetworkedField, AutoPausedField]
    public TimeSpan NextUpdate;
}
