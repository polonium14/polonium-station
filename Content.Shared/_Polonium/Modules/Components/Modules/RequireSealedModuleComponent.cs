using Content.Shared._Polonium.Modules.ModSuit;
using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Modules.Components.Modules;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RequireSealedModuleComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<ModSuitPartType> Parts = [];

    [DataField, AutoNetworkedField]
    public bool RequireAll;

    [DataField, AutoNetworkedField]
    public bool EnableOnSealed;
}
