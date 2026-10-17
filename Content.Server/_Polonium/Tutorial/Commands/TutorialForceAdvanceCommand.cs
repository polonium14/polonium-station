#if TOOLS
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server._Polonium.Tutorial.Commands;

/// <summary>Dev-only: skip the current tutorial step on your mob.</summary>
[AnyCommand]
public sealed partial class TutorialForceAdvanceCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _ent = default!;

    public string Command => "tutorialforceadvance";
    public string Description => "Advances the current tutorial step on your character (dev).";
    public string Help => "Usage: tutorialforceadvance";

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

        if (!_ent.HasComponent<TutorialSessionComponent>(mob))
        {
            shell.WriteError("No active tutorial session on this mob.");
            return;
        }

        _ent.System<TutorialSystem>().ForceAdvance(mob);
        shell.WriteLine("Advanced to next tutorial step.");
    }
}
#endif
