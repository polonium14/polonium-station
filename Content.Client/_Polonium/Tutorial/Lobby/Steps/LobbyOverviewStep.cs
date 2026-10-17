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
        // the character panel is the whole subject of this step. behind an open editor or a
        // collapsed sidebar there is nothing to point at and nothing the player could click
        return StateMan.CurrentState is LobbyState { Lobby: { } lobby }
               && lobby.CharacterPreview is { VisibleInTree: true };
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
            ClickAction = TutorialBubble.ClickBehaviour.CloseOverlay,
            TippyVariant = TutorialBubble.Tippy.ClownRegular,
        };

        bubble.ButtonsContainer.Orientation = BoxContainer.LayoutOrientation.Vertical;
        bubble.ButtonsContainer.Align = BoxContainer.AlignMode.Center;

        var skip = TutorialBubble.MakeButton(Loc.GetString("intro-lobby-skip-button"), primary: false, compact: true);
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

        _lobby.CharacterSetupStateSwitched -= OnSetupOpened;
        _lobby.CharacterSetupStateSwitched += OnSetupOpened;
    }

    /// <summary>
    /// However they got the editor open, this step is done. Hooking the button instead used to
    /// strand anyone whose editor was already up - the button it waits for is behind that window.
    /// </summary>
    private void OnSetupOpened(bool entered, LobbyGui.LobbyGuiState state)
    {
        if (!entered)
            return;

        _lobby.CharacterSetupStateSwitched -= OnSetupOpened;
        Tutorial.NextStep();
    }

    public override void Cleanup()
    {
        base.Cleanup();

        if (_lobby is not null)
            _lobby.CharacterSetupStateSwitched -= OnSetupOpened;
    }
}
