using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Client._Polonium.Tutorial.Lobby.Commands;

[AnyCommand]    
public sealed partial class CancelTutorialCommand : LocalizedCommands
{
    [Dependency] private TutorialManager _tutorial = default!;
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
