// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Client._Polonium.Tutorial.Lobby.Commands;

[AnyCommand]
public sealed class CurrentIntroStepCommand : IConsoleCommand
{
    [Dependency] private readonly TutorialManager _tutorial = default!;

    public string Command => "currentintrostep";

    public string Description => "Echoes the current introduction step.";

    public string Help => $"Usage: {Command} - {Description}";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0)
        {
            shell.WriteLine(Description);
            return;
        }
        if (!_tutorial.IsTutorialActive)
        {
            shell.WriteLine("Not introducting.");
            return;
        }
        var currentStep = _tutorial.CurrentStep;
        shell.WriteLine($"Current step: {currentStep}");
    }
}
