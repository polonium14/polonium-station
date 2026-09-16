using Content.Client._Polonium.Tutorial.Lobby.UI;
using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Polonium.Tutorial.Lobby.Steps;

public sealed class CharacterCreationStep : ClientsideNavTutorialStep
{
    public override string StepId => "character_creation";

    private const float BubbleWidth = 420f;

    private LobbyUIController? _lobby;
    private LobbyGui? _gui;
    private string _saveReminder = string.Empty;

    // survives leaving and re-entering the editor, so a player who closes the window
    // does not have to sit through the panels they already read
    private int _panel;

    /// <summary>One page of the editor tour. Tab is the profile editor tab it belongs to.</summary>
    private sealed record Panel(int Tab, string Message, TutorialBubble.Tippy Tippy = TutorialBubble.Tippy.None);

    private static readonly Panel[] Panels =
    {
        new(0, "intro-character-creation-message-2"),
        new(1, "intro-character-creation-message-3"),
        new(1, "intro-character-creation-message-4", TutorialBubble.Tippy.ClownRegular),
        new(2, "intro-character-creation-message-5"),
        new(3, "intro-character-creation-message-6"),
        new(4, "intro-character-creation-message-7"),
    };

    public override bool CanExecute()
    {
        return StateMan.CurrentState is LobbyState { Lobby.CharacterSetupState.Visible: true };
    }

    public override bool Execute()
    {
        if (StateMan.CurrentState is not LobbyState { Lobby: { CharacterSetupState.Visible: true } gui })
            return false;

        _gui = gui;
        _lobby = UiMan.GetUIController<LobbyUIController>();

        if (_lobby.ProfileEditor is null || _lobby.CharacterSetup is null)
            return false;

        _saveReminder = Loc.GetString("intro-character-creation-save-reminder-message",
            ("humanoid-profile-editor-save-button", Loc.GetString("humanoid-profile-editor-save-button")));

        _gui.CharacterSetupStateSwitched -= OnCharacterSetupStateSwitch;
        _gui.CharacterSetupStateSwitched += OnCharacterSetupStateSwitch;

        // resuming lands straight on a panel, a fresh run gets the intro page first
        if (_panel > 0)
            ShowPanel(_panel);
        else
            ShowIntro();

        return true;
    }

    public override void OnReenter()
    {
        Execute();
    }

    public override void Cleanup()
    {
        base.Cleanup();

        // never leave the editor half locked, even if we got here from a failure
        var editor = _lobby?.ProfileEditor ?? UiMan.GetUIController<LobbyUIController>().ProfileEditor;
        editor?.EnableAllTabs();

        if (_gui is not null)
            _gui.CharacterSetupStateSwitched -= OnCharacterSetupStateSwitch;
    }

    private void ShowIntro()
    {
        var id = $"{StepId}-intro";
        var overlay = TutorialUi.PlanOverlay(id, default, Color.Black.WithAlpha(0.2f), false, true);

        var bubble = new TutorialBubble(Loc.GetString("intro-character-creation-message-1"))
        {
            ClickAction = TutorialBubble.ClickBehaviour.CloseOverlay,
            TippyVariant = TutorialBubble.Tippy.ClownPointing,
        };

        bubble.AnyKeyLabel.LineHeightScale = 0.8f;

        TutorialUi.PlanBubble(bubble, TutorialHighlightOverlay.OverlayControlPosition.BottomRight, spacing: 20f, overlayId: id);

        overlay.InternalOverlayClosedEvent += () => ShowPanel(0);
    }

    private void ShowPanel(int index)
    {
        if (index >= Panels.Length)
        {
            ShowFinale();
            return;
        }

        if (!TryGetEditor(out var editor))
            return;

        _panel = index;
        var panel = Panels[index];

        // keep the player on the tab the page is talking about
        editor.EnableAllTabs();
        editor.OpenTab(panel.Tab);
        editor.DisableAllTabsExcept(panel.Tab);

        var id = $"{StepId}-panel-{index}";
        var overlay = TutorialUi.PlanOverlay(id, default, Color.Transparent, false, false);

        var bubble = new TutorialBubble(FormatPanelMessage(panel.Message), _saveReminder)
        {
            ClickAction = TutorialBubble.ClickBehaviour.Ignore,
            TippyVariant = panel.Tippy,
            MaxWidth = BubbleWidth,
            ButtonsContainer =
            {
                Align = BoxContainer.AlignMode.Center,
                Orientation = BoxContainer.LayoutOrientation.Vertical,
            },
        };

        var continueButton = TutorialBubble.MakeButton(
            Loc.GetString("intro-character-creation-click-to-continue-button"));

        continueButton.OnPressed += _ =>
        {
            overlay.DestroyOverlay();
            ShowPanel(index + 1);
        };

        bubble.ButtonsContainer.AddChild(continueButton);

        TutorialUi.PlanBubble(bubble, TutorialHighlightOverlay.OverlayControlPosition.BottomRight, spacing: 20f, overlayId: id);
    }

    private void ShowFinale()
    {
        if (!TryGetEditor(out var editor))
            return;

        _panel = Panels.Length;
        editor.EnableAllTabs();

        var id = $"{StepId}-finale";
        var overlay = TutorialUi.PlanOverlay(id, editor.SpriteView, Color.FromHex("#65B8E2"), highlightMargin: 4f);

        // last panel, a click anywhere on the bubble is friendlier than one more button
        var bubble = new TutorialBubble(Loc.GetString("intro-character-creation-final-message"))
        {
            ClickAction = TutorialBubble.ClickBehaviour.CloseOverlay,
            TippyVariant = TutorialBubble.Tippy.ClownRegular,
            MaxWidth = BubbleWidth,
        };

        overlay.InternalOverlayClosedEvent += EndStep;

        TutorialUi.PlanBubble(bubble, TutorialHighlightOverlay.OverlayControlPosition.CenterLeft, editor.SpriteView, spacing: 20f, overlayId: id);
    }

    private void EndStep()
    {
        if (!TryGetEditor(out var editor))
            return;

        editor.EnableAllTabs();

        // stop the close handler from firing while the editor shuts down behind us
        if (_gui is not null)
            _gui.CharacterSetupStateSwitched -= OnCharacterSetupStateSwitch;

        Tutorial.NextStep();
    }

    private string FormatPanelMessage(string message)
    {
        // a few pages name the tab they are pointing at, feed them the live tab captions
        return message switch
        {
            "intro-character-creation-message-3" => Loc.GetString(message,
                ("humanoid-profile-editor-jobs-tab", Loc.GetString("humanoid-profile-editor-jobs-tab"))),
            "intro-character-creation-message-6" => Loc.GetString(message,
                ("humanoid-profile-editor-traits-tab", Loc.GetString("humanoid-profile-editor-traits-tab"))),
            "intro-character-creation-message-7" => Loc.GetString(message,
                ("humanoid-profile-editor-markings-tab", Loc.GetString("humanoid-profile-editor-markings-tab"))),
            _ => Loc.GetString(message),
        };
    }

    private bool TryGetEditor(out HumanoidProfileEditor editor)
    {
        if (_lobby?.ProfileEditor is { } found && _lobby.CharacterSetup is not null)
        {
            editor = found;
            return true;
        }

        editor = default!;
        CancelWithError("Character editor went away mid step.");
        return false;
    }

    /// <summary>
    /// Closing the editor used to kill the whole tutorial. Step back to the lobby overview instead,
    /// which points at the button that reopens it - the tour then resumes on the same panel.
    /// </summary>
    private void OnCharacterSetupStateSwitch(bool entered, LobbyGui.LobbyGuiState state)
    {
        if (entered)
            return;

        if (!Tutorial.PreviousStep())
            Tutorial.CancelTutorial();
    }
}
