#if DEBUG
using Content.Client._Polonium.Pathfinding;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Client.Player;
using Robust.Shared.IoC;
using Content.Shared.Administration;

namespace Content.Client._Polonium.Pathfinding.Commands;

[AnyCommand]
public sealed class TestPathfindingCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public string Command => "testpathfinding";
    public string Description => "patrzymy scieżkę od gracza do podanej encji";
    public string Help => "Użycie: testpathfinding <uid encji>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError("Zła liczba argumentów");
            return;
        }

        if (!NetEntity.TryParse(args[0], out var targetNetEntity))
        {
            shell.WriteError($"Nieprawidłowy ID encji: {args[0]}");
            return;
        }

        if (_playerManager.LocalEntity == null)
        {
            shell.WriteError("Nie znaleziono lokalnego gracza");
            return;
        }

        // Tłumaczenie po id na lokalne Uid encji
        if (!_entManager.TryGetEntity(targetNetEntity, out var targetUid) || !_entManager.EntityExists(targetUid))
        {
            shell.WriteError("Taka encja nie istnieje lub nie jest dostępna dla klienta.");
            return;
        }

        var player = _playerManager.LocalEntity.Value;
        var pathComp = _entManager.EnsureComponent<PlayerPathfindingComponent>(player);
        pathComp.Destination = targetUid;
        pathComp.Active = true;
    }
}

[AnyCommand]
public sealed class ClearTestPathfindingCommand : IConsoleCommand
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;

    public string Command => "clear_pathfinding";
    public string Description => "Wyłącza pathfinding";
    public string Help => "Użycie: clear_pathfinding";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_playerManager.LocalEntity == null)
        {
            shell.WriteError("Nie znaleziono lokalnego gracza");
            return;
        }

        if (!_entManager.TryGetComponent<PlayerPathfindingComponent>(
            _playerManager.LocalEntity.Value, out var pathComp
            ))
            return;

        pathComp.Active = false;
        pathComp.Destination = null;
        pathComp.CurrentPath.Clear();
    }
}
#endif
