using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Modules.Components.Modules;

/// <summary>
/// A storage module. Its items are moved into the storage of the suit it is installed in, and back when it is removed.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class StorageModuleComponent : Component;
