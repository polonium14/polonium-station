using System.Linq;
using Content.Server.Administration;
using Content.Server.GameTicking;
using Content.Server.Shuttles.Systems;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.EvacScenarios;

/// <summary>
/// Shows the evac flight scenarios, picks the one this round's evacuation runs, or sends the shuttle off right away
/// to try one out.
/// </summary>
[AdminCommand(AdminFlags.Round)]
public sealed partial class EvacScenarioCommand : LocalizedEntityCommands
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private EmergencyShuttleSystem _emergency = default!;
    [Dependency] private EvacScenarioSystem _scenario = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;

    private const string NoScenario = "none";

    public override string Command => "evacscenario";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        switch (args.Length == 0 ? "list" : args[0])
        {
            case "list" when args.Length <= 1:
                List(shell);
                break;
            case "force" when args.Length == 2:
                Force(shell, args[1]);
                break;
            case "clear" when args.Length == 1:
                _scenario.ClearForcedScenario();
                shell.WriteLine(Loc.GetString("cmd-evacscenario-cleared"));
                break;
            case "test" when args.Length == 2:
                Test(shell, args[1]);
                break;
            default:
                shell.WriteError(Help);
                break;
        }
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        switch (args.Length)
        {
            case 1:
                return CompletionResult.FromHintOptions(new[] { "list", "force", "clear", "test" },
                    Loc.GetString("cmd-evacscenario-arg-action"));
            case 2 when args[0] is "force" or "test":
                var options = _scenario.GetScenarios().Select(s => s.Proto.ID);
                if (args[0] == "force")
                    options = options.Append(NoScenario);

                return CompletionResult.FromHintOptions(options, Loc.GetString("cmd-evacscenario-arg-scenario"));
            default:
                return CompletionResult.Empty;
        }
    }

    private void List(IConsoleShell shell)
    {
        var any = false;

        foreach (var (proto, scenario) in _scenario.GetScenarios())
        {
            any = true;
            shell.WriteLine(Loc.GetString("cmd-evacscenario-list-entry",
                ("id", proto.ID),
                ("chance", MathF.Round(_scenario.GetChance(scenario) * 100f, 2))));
        }

        if (!any)
            shell.WriteLine(Loc.GetString("cmd-evacscenario-list-empty"));

        if (!_scenario.IsForced)
            shell.WriteLine(Loc.GetString(_scenario.RollingEnabled ? "cmd-evacscenario-state-random" : "cmd-evacscenario-state-disabled"));
        else if (_scenario.ForcedScenario is { } forced)
            shell.WriteLine(Loc.GetString("cmd-evacscenario-state-forced", ("id", forced.Id)));
        else
            shell.WriteLine(Loc.GetString("cmd-evacscenario-state-forced-none"));
    }

    private void Force(IConsoleShell shell, string id)
    {
        if (id == NoScenario)
        {
            _scenario.ForceScenario(null);
            shell.WriteLine(Loc.GetString("cmd-evacscenario-forced-none"));
            return;
        }

        if (!IsScenario(id))
        {
            shell.WriteError(Loc.GetString("cmd-evacscenario-unknown", ("id", id)));
            return;
        }

        _scenario.ForceScenario(id);
        shell.WriteLine(Loc.GetString("cmd-evacscenario-forced", ("id", id)));
    }

    /// <summary>
    /// Calls the emergency shuttle in, sends it off at once with the scenario and puts the caller aboard.
    /// </summary>
    private void Test(IConsoleShell shell, string id)
    {
        if (!IsScenario(id))
        {
            shell.WriteError(Loc.GetString("cmd-evacscenario-unknown", ("id", id)));
            return;
        }

        if (_ticker.RunLevel != GameRunLevel.InRound)
        {
            shell.WriteError(Loc.GetString("cmd-evacscenario-not-in-round"));
            return;
        }

        if (_emergency.ShuttlesLeft)
        {
            shell.WriteError(Loc.GetString("cmd-evacscenario-already-left"));
            return;
        }

        // Dev builds turn evac off, and docking without it would just end the round.
        if (!_cfg.GetCVar(CCVars.EmergencyShuttleEnabled))
        {
            _cfg.SetCVar(CCVars.EmergencyShuttleEnabled, true);
            shell.WriteLine(Loc.GetString("cmd-evacscenario-evac-enabled"));
        }

        _scenario.ForceScenario(id);
        _emergency.DockEmergencyShuttle();
        _emergency.EarlyLaunch();

        // Hands back an invalid uid rather than null when there is none.
        if (_emergency.GetShuttle() is not { Valid: true } shuttle)
        {
            _scenario.ClearForcedScenario();
            shell.WriteError(Loc.GetString("cmd-evacscenario-no-shuttle"));
            return;
        }

        shell.WriteLine(Loc.GetString("cmd-evacscenario-test-started", ("id", id)));

        if (shell.Player?.AttachedEntity is { } player && TryFindFreeSpot(shuttle, out var spot))
        {
            _transform.SetMapCoordinates(player, _transform.ToMapCoordinates(spot));
            _transform.AttachToGridOrMap(player);
            shell.WriteLine(Loc.GetString("cmd-evacscenario-test-boarded"));
        }
    }

    private bool IsScenario(string id)
    {
        return _proto.TryIndex<EntityPrototype>(id, out var proto)
               && !proto.Abstract
               && proto.HasComp<EvacScenarioComponent>(EntityManager.ComponentFactory);
    }

    private bool TryFindFreeSpot(EntityUid shuttle, out EntityCoordinates spot)
    {
        spot = default;
        if (!EntityManager.TryGetComponent(shuttle, out MapGridComponent? grid))
            return false;

        var middle = grid.LocalAABB.Center;
        var best = float.MaxValue;
        var found = false;

        foreach (var tile in _map.GetAllTiles(shuttle, grid))
        {
            var local = _map.GridTileToLocal(shuttle, grid, tile.GridIndices);
            var distance = (local.Position - middle).LengthSquared();
            if (distance >= best || _turf.IsSpace(tile) || _turf.IsTileBlocked(tile, CollisionGroup.MobMask))
                continue;

            best = distance;
            spot = local;
            found = true;
        }

        return found;
    }
}
