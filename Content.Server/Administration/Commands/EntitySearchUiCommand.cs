// SPDX-FileCopyrightText: 2025 Polonium Station Contributors
//
// SPDX-License-Identifier: MIT

using Content.Server.Administration.UI;
using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Administration.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class EntitySearchUiCommand : IConsoleCommand
{
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

        var eui = IoCManager.Resolve<EuiManager>();
        eui.OpenEui(new EntitySearchEui(), player);
    }
}
