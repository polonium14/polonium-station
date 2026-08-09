// SPDX-FileCopyrightText: 2025 nikitosych <174215049+nikitosych@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Copilot <175728472+Copilot@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using Content.Shared.Administration;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Shared.Console;

namespace Content.Client._Polonium.Tutorial.Lobby.Commands;

[AnyCommand]
public sealed class StartTutorialCommand : LocalizedCommands
{
    [Dependency] private readonly TutorialManager _tutorial = default!;
    [Dependency] private readonly IStateManager _stateMan = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    public override string Command => "startintro";
    public override string Help => Loc.GetString("cmd-startintro-help", ("command", Command));
    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0)
        {
            shell.WriteLine(Help);
            return;
        }

        if (_stateMan.CurrentState is not LobbyState lobby)
        {
            shell.WriteError(Loc.GetString("cmd-startintro-not-in-lobby"));
            return;
        }

        _ui.ClearWindows();
        lobby.Lobby?.SwitchState(LobbyGui.LobbyGuiState.Default);

        _tutorial.StartTutorial();
    }
}

