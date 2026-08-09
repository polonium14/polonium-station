// SPDX-FileCopyrightText: 2025 Copilot <175728472+Copilot@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Client._Polonium.Tutorial.Lobby.Commands;

[AnyCommand]    
public sealed class CancelTutorialCommand : LocalizedCommands
{
    [Dependency] private readonly TutorialManager _tutorial = default!;
    public override string Command => "cancelintro";
    public override string Help => LocalizationManager.GetString($"cmd-cancelintro-help", ("command", Command));
    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0)
        {
            shell.WriteLine(Help);
            return;
        }

        _tutorial.CancelTutorial();
    }
}
