using Content.Server.Administration.UI;
using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.IoC;

namespace Content.Server.Administration.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class EntitySearchUiCommand : IConsoleCommand
{
    [Dependency] private readonly IDependencyCollection _deps = default!;
    [Dependency] private readonly EuiManager _eui = default!;

    public string Command => "entitysearchui";

    public string Description => "Opens the admin entity search panel.";

    public string Help => Command;

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var player = shell.Player;
        if (player == null)
        {
            shell.WriteLine("This does not work from the server console.");
            return;
        }

        _eui.OpenEui(new EntitySearchEui(_deps), player);
    }
}
