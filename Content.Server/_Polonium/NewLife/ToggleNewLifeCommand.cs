using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Robust.Shared.Configuration;
using Robust.Shared.Console;

namespace Content.Server._Polonium.NewLife;

[AdminCommand(AdminFlags.Admin)]
public sealed partial class ToggleNewLifeCommand : LocalizedCommands
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    public override string Command => "togglenewlife";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        bool enabled;
        switch (args.Length)
        {
            case 0:
                enabled = !_cfg.GetCVar(CCVars.NewLifeEnabled);
                break;
            case 1 when bool.TryParse(args[0], out var parsed):
                enabled = parsed;
                break;
            default:
                shell.WriteError(Loc.GetString("shell-invalid-bool"));
                return;
        }

        _cfg.SetCVar(CCVars.NewLifeEnabled, enabled);

        _adminLog.Add(LogType.Respawn,
            LogImpact.High,
            $"{shell.Player?.Name ?? "Server"} {(enabled ? "enabled" : "disabled")} new lives.");

        shell.WriteLine(Loc.GetString(enabled ? "cmd-togglenewlife-enabled" : "cmd-togglenewlife-disabled"));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1
            ? CompletionResult.FromHintOptions(CompletionHelper.Booleans, Loc.GetString("cmd-togglenewlife-arg-enabled"))
            : CompletionResult.Empty;
    }
}
