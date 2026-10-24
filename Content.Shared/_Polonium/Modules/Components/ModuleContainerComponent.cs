using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Modules.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ModuleContainerComponent : Component
{
    [DataField]
    public string ModuleContainerId = "modules";

    [ViewVariables]
    public Container Modules = new();

    [DataField, AutoNetworkedField]
    public EntityWhitelist? Whitelist;

    [DataField, AutoNetworkedField]
    public EntityWhitelist? Blacklist;

    /// <summary>
    /// How long it takes a player to install a module.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan InsertDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How long it takes a player to take a module out.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan RemoveDelay = TimeSpan.FromSeconds(4);
}
