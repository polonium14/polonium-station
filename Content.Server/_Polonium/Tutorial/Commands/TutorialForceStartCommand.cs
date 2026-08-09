// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

#if TOOLS
using Content.Shared._Polonium.Tutorial.Prototypes;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Tutorial.Commands;

/// <summary>Dev-only: start TutorialBasic on your mob without going through SolitarySpawning.</summary>
[AnyCommand]
public sealed class TutorialForceStartCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _ent = default!;

    public string Command => "tutorialforcestart";
    public string Description => "Starts the in-game tutorial flow on your character (dev).";
    public string Help => "Usage: tutorialforcestart [flowId]\nDefault flow: TutorialBasic";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError("You must be in-game to run this command.");
            return;
        }

        if (player.AttachedEntity is not { } mob)
        {
            shell.WriteError("No attached entity.");
            return;
        }

        var flow = args.Length > 0
            ? new ProtoId<TutorialFlowPrototype>(args[0])
            : new ProtoId<TutorialFlowPrototype>("TutorialBasic");

        _ent.System<TutorialSystem>().ForceStartFlow(mob, flow);
        shell.WriteLine($"Started tutorial flow '{flow}' on {mob}.");
    }
}
#endif
