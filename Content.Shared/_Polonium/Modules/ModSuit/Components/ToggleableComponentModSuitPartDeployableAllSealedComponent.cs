using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Modules.ModSuit.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ToggleableComponentModSuitPartDeployableAllSealedComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public bool AllSealed;

    [DataField, AlwaysPushInheritance]
    public ComponentRegistry? DeployerComponents;

    [DataField, AlwaysPushInheritance]
    public ComponentRegistry? AllPartComponents;
}
