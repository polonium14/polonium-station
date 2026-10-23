using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Modules.ModSuit.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ModSuitPartTypeComponent : Component
{
    [DataField, AutoNetworkedField]
    public ModSuitPartType Type;
}
