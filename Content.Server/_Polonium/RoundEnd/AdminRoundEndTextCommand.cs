using Content.Server.Administration;
using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Polonium.RoundEnd;

[AdminCommand(AdminFlags.Round)]
public sealed partial class AdminRoundEndTextCommand : LocalizedCommands
{
    [Dependency] private IDependencyCollection _deps = default!;
    [Dependency] private EuiManager _eui = default!;

    public override string Command => "roundendtext";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } admin)
        {
            shell.WriteError(Loc.GetString("cmd-roundendtext-server"));
            return;
        }

        _eui.OpenEui(new AdminRoundEndTextEui(_deps), admin);
    }
}
