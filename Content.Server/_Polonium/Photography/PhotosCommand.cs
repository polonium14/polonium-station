using Content.Server.Administration;
using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Polonium.Photography;

/// <summary>Opens the admin photo viewer to review and delete this round's captured photos.</summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class PhotosCommand : IConsoleCommand
{
    [Dependency] private readonly EuiManager _eui = default!;

    public string Command => "photos";
    public string Description => "Open the admin viewer for photos captured with cameras this round.";
    public string Help => "photos";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        _eui.OpenEui(new AdminPhotoEui(), player);
    }
}
