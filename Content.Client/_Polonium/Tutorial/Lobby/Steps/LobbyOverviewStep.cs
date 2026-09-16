using Content.Client.Lobby;
using Content.Shared._Polonium.Tutorial.Lobby;
using Robust.Client.ResourceManagement;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Content.Client._Polonium.Tutorial.Lobby.UI;
using Content.Client.Lobby.UI;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Polonium.Tutorial.Lobby.Steps;

public sealed class LobbyOverviewStep : ClientsideNavTutorialStep
{
    public override string StepId => "lobby_overview";

    private LobbyGui _lobby = default!;

    public override bool Execute()
    {
        if (StateMan.CurrentState is not LobbyState { Lobby: { } lobby })
            return false;

        _lobby = lobby;

        // we have to execute only first overlay
        FirstOverlay();

        return true;
    }

    public override bool CanExecute()
    {
        return StateMan.CurrentState is LobbyState;
    }

    /// <summary>
    /// Coming back here means the player closed the character editor. Skip the room tour they
    /// already saw and go straight to the part that points at the button they need.
    /// </summary>
    public override void OnReenter()
    {
        if (StateMan.CurrentState is not LobbyState { Lobby: { } lobby })
            return;

        _lobby = lobby;
        SecondOverlay(reentry: true);
    }

    private void FirstOverlay()
    {
        var name = $"{StepId}-1";
        var overlay = TutorialUi.PlanOverlay(name, _lobby.RightSide, Color.Green, isSelfClosingOnClick: true, ignoreHighlightClicks: true);

        var bubble = new TutorialBubble(Loc.GetString("intro-lobby-overview-message-1"))
        {
            ClickAction = TutorialBubble.ClickBehaviour.Ignore,
            TippyVariant = TutorialBubble.Tippy.ClownRegular,
        };

        var skip = TutorialBubble.MakeButton(Loc.GetString("intro-lobby-skip-button"), primary: false);
        skip.OnPressed += _ => Tutorial.SkipLobbyTour();
        bubble.ButtonsContainer.AddChild(skip);

        TutorialUi.PlanBubble(bubble, TutorialHighlightOverlay.OverlayControlPosition.CenterLeft, _lobby.RightSide, overlayId: name);

        overlay.InternalOverlayClosedEvent += () =>
        {
            if (Tutorial.IsTutorialActive && Tutorial.ActiveStep?.StepId == StepId)
                SecondOverlay();
        };
    }

    private void SecondOverlay(bool reentry = false)
    {
        var second = $"{StepId}-2";
        var cp = _lobby.CharacterPreview;

        TutorialUi.PlanOverlay(second, cp, Color.Green, isSelfClosingOnClick: false);

        var buttonText = cp!.CharacterSetupButton.Text ?? string.Empty;

        // returning here means the editor was closed, do not replay the whole room tour at them
        var bubble = reentry
            ? new TutorialBubble(
                Loc.GetString("intro-character-creation-reopen-message",
                    ("intro-lobby-overview-character-editor-button", buttonText)))
            {
                ClickAction = TutorialBubble.ClickBehaviour.Ignore,
                TippyVariant = TutorialBubble.Tippy.ClownPointing,
            }
            : new TutorialBubble(
                Loc.GetString("intro-lobby-overview-character-section-message-1"),
                Loc.GetString("intro-lobby-overview-character-section-message-2",
                    ("intro-lobby-overview-character-editor-button", buttonText)))
            {
                ClickAction = TutorialBubble.ClickBehaviour.Ignore,
                TippyVariant = TutorialBubble.Tippy.ClownRegular,
            };

        TutorialUi.PlanBubble(bubble, TutorialHighlightOverlay.OverlayControlPosition.CenterLeft, cp, overlayId: second);

        cp.CharacterSetupButton.OnPressed -= SetupPressed;
        cp.CharacterSetupButton.OnPressed += SetupPressed;
    }

    private void SetupPressed(BaseButton.ButtonEventArgs _)
    {
        _lobby.CharacterPreview.CharacterSetupButton.OnPressed -= SetupPressed;
        Tutorial.NextStep();
    }

    public override void Cleanup()
    {
        base.Cleanup();

        if (_lobby?.CharacterPreview?.CharacterSetupButton is { } button)
        {
            button.OnPressed -= SetupPressed;
        }
    }
}
