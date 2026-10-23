using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Modules.Components.Modules;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StorageModuleComponent : Component
{
    public const string ContainerId = "modsuit-storage-module";

    [ViewVariables]
    public Container Items;

    [ViewVariables, AutoNetworkedField]
    public Dictionary<EntityUid, ItemStorageLocation> Locations = [];
}
