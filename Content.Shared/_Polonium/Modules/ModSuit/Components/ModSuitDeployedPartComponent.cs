using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Modules.ModSuit.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ModSuitDeployedPartComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public EntityUid Suit;

    [ViewVariables, AutoNetworkedField]
    public string Slot;
}
