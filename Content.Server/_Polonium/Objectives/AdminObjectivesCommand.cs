using System.Linq;
using Content.Server.Administration;
using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;

namespace Content.Server._Polonium.Objectives;

[AdminCommand(AdminFlags.Admin)]
public sealed partial class AdminObjectivesCommand : LocalizedCommands
{
    [Dependency] private IDependencyCollection _deps = default!;
    [Dependency] private EuiManager _eui = default!;
    [Dependency] private IPlayerManager _players = default!;

    public override string Command => "objectives";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } admin)
        {
            shell.WriteError(Loc.GetString("cmd-objectives-server"));
            return;
        }

        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("cmd-objectives-invalid-args"));
            return;
        }

        if (!_players.TryGetSessionByUsername(args[0], out var target))
        {
            shell.WriteError(Loc.GetString("cmd-objectives-player-not-found"));
            return;
        }

        _eui.OpenEui(new AdminObjectivesEui(_deps, target.UserId, target.Name), admin);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length != 1)
            return CompletionResult.Empty;

        var options = _players.Sessions.OrderBy(c => c.Name).Select(c => c.Name).ToArray();
        return CompletionResult.FromHintOptions(options, Loc.GetString("cmd-objectives-completion"));
    }
}
