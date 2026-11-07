using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.TestChamber;

/// <summary>
/// Console that drops <see cref="TestChamberKitPrototype"/> kits around itself and cleans them up after testing.
/// On a development server it is spawned next to the first player that joins, see CCVars.TestChamberConsole.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TestChamberConsoleComponent : Component
{
    /// <summary>
    /// How far from the console kits are laid out.
    /// </summary>
    [DataField]
    public int Range = 6;
}

[Serializable, NetSerializable]
public enum TestChamberUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class TestChamberSpawnKitMessage(string kit) : BoundUserInterfaceMessage
{
    public readonly string Kit = kit;
}

[Serializable, NetSerializable]
public sealed class TestChamberResetMessage : BoundUserInterfaceMessage;

/// <summary>
/// Raised broadcast after a kit has been dropped, for kits that need extra setup.
/// </summary>
public sealed class TestChamberKitSpawnedEvent(string kit, EntityUid console, EntityUid? user, List<EntityUid> spawned) : EntityEventArgs
{
    public readonly string Kit = kit;
    public readonly EntityUid Console = console;
    public readonly EntityUid? User = user;
    public readonly List<EntityUid> Spawned = spawned;
}
