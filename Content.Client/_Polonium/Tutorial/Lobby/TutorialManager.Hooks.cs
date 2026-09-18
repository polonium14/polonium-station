using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using TutorialPresentationSystem = Content.Client._Polonium.Tutorial.TutorialPresentationSystem;
using Content.Shared._Polonium.Tutorial;
using Robust.Client;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;

namespace Content.Client._Polonium.Tutorial.Lobby;

/// <summary>
/// Where the client tells us what it is doing: entering the lobby, dismissing the rules,
/// changing state or round level. Nothing starts until the lobby is really on screen.
/// </summary>
public sealed partial class TutorialManager
{
    private void OnLobbyEntered(LobbyGui gui)
    {
        if (gui.LinksBanner.TutorialButton is { } button)
        {
            button.Disabled = false;
        }

        // a cancelled or crashed run can leave the editor half locked, always undo that here
        _lobby.ProfileEditor?.EnableAllTabs();

        TryBeginLobbyIntro();
    }

    private void OnRulesPopupChanged()
    {
        if (_info.IsRulesPopupOpen)
        {
            if (IsTutorialActive && !_isPaused)
                PauseTutorial();

            CloseTrainingOffer();
            return;
        }

        TryBeginLobbyIntro();
    }

    private void TryBeginLobbyIntro()
    {
        if (_info.IsRulesPopupOpen || !_info.RulesReady)
            return;

        if (_stateMan.CurrentState is not LobbyState)
            return;

        var mode = GetIntroMode();
        if (mode == SharedTutorialSystem.IntroNone)
            return;

        if (mode == SharedTutorialSystem.IntroTutorial)
        {
            if (IsPaused && IsTutorialActive)
            {
                ResumeTutorial();
                return;
            }

            if (!IsTutorialActive && !Progress.IsCompleted)
                Start(null);
            return;
        }

        if (IsTutorialActive)
            CancelTutorial();

        TryOfferTraining();
    }

    private void OnStateChanged(StateChangedEventArgs args)
    {
        if (args.NewState is LobbyState { Lobby: { } gui })
        {
            OnLobbyEntered(gui);
            return;
        }

        CloseTrainingOffer();
        CloseTrainingHopWindow();

        if (IsTutorialActive)
            PauseTutorial();

        // skip-later / restart-hint sit on RootControl. if they survive into gameplay
        // PlanOverlay just queues the welcome bubble behind them and never draws it
        if (_tutorialUi.ActiveOverlay is { } leftover
            && leftover.Id != TutorialPresentationSystem.OverlayId)
            _tutorialUi.DiscardActive();
    }

    private void OnRunLevelChanged(object? sender, RunLevelChangedEventArgs args)
    {
        if (args.NewLevel != ClientRunLevel.Initialize)
            return;

        _dbCompleted = null;
        _lobbyTourSent = false;

        // a new connection is a new run of the tour, bubble size step included
        if (IsTutorialActive)
            CancelTutorial();
        Progress.IsCompleted = false;
        CloseTrainingOffer();
        CloseTrainingHopWindow();
        _hopWindow = null;
    }
}
