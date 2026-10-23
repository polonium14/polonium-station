using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Modules.ModSuit.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ModSuitComplexityLimitComponent : Component
{
    [DataField, AutoNetworkedField]
    public int MaxComplexity;

    [ViewVariables, AutoNetworkedField, Access(typeof(SharedModSuitSystem))]
    public int Complexity;
}
