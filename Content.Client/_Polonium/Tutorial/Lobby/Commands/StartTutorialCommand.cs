using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using Content.Shared._Polonium.Tutorial;
using Content.Shared.Administration;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Shared.Console;

namespace Content.Client._Polonium.Tutorial.Lobby.Commands;

[AnyCommand]
public sealed partial class StartTutorialCommand : LocalizedCommands
{
    [Dependency] private TutorialManager _tutorial = default!;
    [Dependency] private IStateManager _stateMan = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    public override string Command => "startintro";
    public override string Help => Loc.GetString("cmd-startintro-help", ("command", Command));
    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0)
        {
            shell.WriteLine(Help);
            return;
        }

        if (_tutorial.GetIntroMode() == SharedTutorialSystem.IntroNone)
        {
            shell.WriteError(Loc.GetString("cmd-startintro-disabled"));
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

