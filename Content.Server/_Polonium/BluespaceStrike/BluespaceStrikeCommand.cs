// SPDX-FileCopyrightText: 2026 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration;
using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Polonium.BluespaceStrike;

[AdminCommand(AdminFlags.Fun)]
public sealed partial class BluespaceStrikeCommand : LocalizedEntityCommands
{
    [Dependency] private EuiManager _euiManager = default!;

    public override string Command => "artbs";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var player = shell.Player;
        if (player == null)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        _euiManager.OpenEui(new BluespaceStrikeEui(), player);
    }
}
