using Content.Shared._Polonium.Modules.ModSuit;
using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Modules.Components.Modules;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ModSuitDeployableContainerVisualsComponent : Component
{
    [DataField, AutoNetworkedField]
    public ModSuitPartType PartType;
}
