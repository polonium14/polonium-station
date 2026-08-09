// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client._Polonium.Tutorial.Lobby.UI;
using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using Content.Client.Lobby.UI.ProfileEditorControls;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using System.Linq;
using Robust.Client.UserInterface;

namespace Content.Client._Polonium.Tutorial.Lobby.Steps;
public sealed class CharacterCreationStep : ClientsideNavTutorialStep
{
    public override string StepId => "character_creation";

    private LobbyUIController? _lobby;
    private LobbyGui? _gui;

    private BoxContainer? _charactersContainer;
    private string _saveReminderMessage = default!;

    public override bool Execute()
    {
        if (StateMan.CurrentState is not LobbyState { Lobby: { CharacterSetupState: { Visible: true } characterSetup } gui })
            return false;

        _gui = gui;
        _lobby = UiMan.GetUIController<LobbyUIController>();

        if (_lobby.ProfileEditor is null)
            return false;

        if (_lobby.CharacterSetup is null)
            return false;

        _gui.CharacterSetupStateSwitched += OnCharacterSetupStateSwitch;

        _saveReminderMessage = Loc.GetString("intro-character-creation-save-reminder-message", ("humanoid-profile-editor-save-button", Loc.GetString("humanoid-profile-editor-save-button")));

        FirstOverlay();

        return true;
    }

    public override void OnReenter()
    {
        Execute();
    }

    public override bool CanExecute()
    {
        return StateMan.CurrentState is LobbyState { Lobby: { CharacterSetupState: { Visible: true } } };
    }

    public override void Cleanup()
    {
        base.Cleanup();

        UnsubscribeFromCharacterButtons();

        if (_lobby!.ProfileEditor is { } editor)
        {
            editor.EnableAllTabs();
        }

        if (_gui is not null)
        {
            _gui.CharacterSetupStateSwitched -= OnCharacterSetupStateSwitch;
        }
    }

    private void SubscribeToCharacterButtons()
    {
        if (_charactersContainer == null)
            return;

        foreach (var child in _charactersContainer.Children.OfType<CharacterPickerButton>())
        {
            child.OnPressed -= ProceedToSixth;
            child.OnPressed += ProceedToSixth;
        }
    }

    private void UnsubscribeFromCharacterButtons()
    {
        if (_charactersContainer == null)
            return;

        _charactersContainer.OnChildAdded -= OnCharacterButtonAdded;
        _charactersContainer.OnChildRemoved -= OnCharacterButtonRemoved;

        foreach (var child in _charactersContainer.Children.OfType<CharacterPickerButton>())
        {
            child.OnPressed -= ProceedToSixth;
        }

        _charactersContainer = null;
    }

    private void OnCharacterButtonAdded(Control child)
    {
        if (child is CharacterPickerButton button)
        {
            button.OnPressed -= ProceedToSixth;
            button.OnPressed += ProceedToSixth;
        }
    }

    private void OnCharacterButtonRemoved(Control child)
    {
        if (child is CharacterPickerButton button)
        {
            button.OnPressed -= ProceedToSixth;
        }
    }

    // Rozumiem, że realizacja dziesięciu prawie identycznych funkcji to ZŁY pomysł, ale przynajmniej działa i relatywnie łatwo się debuguje. Być może później uda się to wszystko ładniej strukturyzować.

    private void FirstOverlay()
    {
        if (_lobby!.CharacterSetup is not { } setup)
        {
            CancelWithError("Cannot access the character setup window. Cancelling introduction.");
            return;
        }

        var chars = setup.FindControl<BoxContainer>("Characters");
        var name = $"{StepId}-1";
        var overlay = TutorialUi.PlanOverlay(name, default, Color.Black.WithAlpha(0.2f), false, true);

        var bubble = new TutorialBubble(Loc.GetString("intro-character-creation-message-1"))
        {
            ClickAction = TutorialBubble.ClickBehaviour.CloseOverlay,
            TippyVariant = TutorialBubble.Tippy.ClownPointing,
        };

        bubble.AnyKeyLabel.LineHeightScale = 0.8f;

        TutorialUi.PlanBubble(bubble, TutorialHighlightOverlay.OverlayControlPosition.BottomLeft, spacing: 20f, overlayId: name);

        overlay.InternalOverlayClosedEvent += SecondOverlay;
    }

    private void SecondOverlay()
    {
        if (_lobby!.ProfileEditor is not { } editor)
        {
            CancelWithError("Cannot access the profile editor. Cancelling introduction.");
            return;
        }
        if (_lobby!.CharacterSetup is not { } setup)
        {
            CancelWithError("Cannot access the character setup window. Cancelling introduction.");
            return;
        }

        editor.OpenTab(0);
        editor.DisableAllTabsExcept(0);

        var chars = setup.FindControl<BoxContainer>("Characters");
        var name = $"{StepId}-2";
        var overlay = TutorialUi.PlanOverlay(name, default, Color.Transparent, false, false);

        var bubble = new TutorialBubble(
            Loc.GetString("intro-character-creation-message-2"),
            _saveReminderMessage)
        {
            ClickAction = TutorialBubble.ClickBehaviour.Ignore,
            TippyVariant = TutorialBubble.Tippy.None,
            MaxWidth = (chars?.PixelWidth ?? 375) - (chars?.GlobalPixelPosition.X ?? 0),
            ButtonsContainer =
            {
                Align = BoxContainer.AlignMode.Center,
                Orientation = BoxContainer.LayoutOrientation.Vertical,
            },
        };

        var continueButton = new Button
        {
            Text = Loc.GetString("intro-character-creation-click-to-continue-button"),
        };

        continueButton.OnPressed += _ =>
        {
            overlay.DestroyOverlay();
            ThirdOverlay();
        };

        bubble.ButtonsContainer.AddChild(continueButton);

        TutorialUi.PlanBubble(bubble, TutorialHighlightOverlay.OverlayControlPosition.BottomLeft, spacing: 20f, overlayId: name);
    }

    private void ThirdOverlay()
    {
        if (_lobby!.ProfileEditor is not { } editor)
        {
            CancelWithError("Cannot access the profile editor. Cancelling introduction.");
            return;
        }
        if (_lobby!.CharacterSetup is not { } setup)
        {
            CancelWithError("Cannot access the character setup window. Cancelling introduction.");
            return;
        }

        editor.EnableAllTabs();

        editor.OpenTab(1);

        editor.DisableAllTabsExcept(1);

        var chars = setup.FindControl<BoxContainer>("Characters");
        var name = $"{StepId}-3";
        var overlay = TutorialUi.PlanOverlay(name, default, Color.Transparent, false, false);

        var bubble = new TutorialBubble(
            Loc.GetString("intro-character-creation-message-3", ("humanoid-profile-editor-jobs-tab", Loc.GetString("humanoid-profile-editor-jobs-tab"))),
            _saveReminderMessage)
        {
            ClickAction = TutorialBubble.ClickBehaviour.Ignore,
            TippyVariant = TutorialBubble.Tippy.None,
            MaxWidth = (chars?.PixelWidth ?? 375) - (chars?.GlobalPixelPosition.X ?? 0),
            ButtonsContainer =
            {
                Align = BoxContainer.AlignMode.Center,
                Orientation = BoxContainer.LayoutOrientation.Vertical,
            },
        };

        var continueButton = new Button
        {
            Text = Loc.GetString("intro-character-creation-click-to-continue-button"),
        };

        continueButton.OnPressed += _ =>
        {
            overlay.DestroyOverlay();
            FourthOverlay();
        };

        bubble.ButtonsContainer.AddChild(continueButton);

        TutorialUi.PlanBubble(bubble, TutorialHighlightOverlay.OverlayControlPosition.BottomLeft, spacing: 20f, overlayId: name);
    }

    private void FourthOverlay()
    {
        // Job priorities live on the Jobs tab now - no separate JobPriorityEditor
        if (_lobby!.ProfileEditor is not { } editor)
        {
            CancelWithError("Cannot access the profile editor. Cancelling introduction.");
            return;
        }

        if (_lobby!.CharacterSetup is not { } setup)
        {
            CancelWithError("Cannot access the character setup window. Cancelling introduction.");
            return;
        }

        editor.EnableAllTabs();
        editor.OpenTab(1);
        editor.DisableAllTabsExcept(1);

        var chars = setup.FindControl<BoxContainer>("Characters");
        var name = $"{StepId}-4";
        var overlay = TutorialUi.PlanOverlay(name, default, Color.Transparent, false, false);

        var bubble = new TutorialBubble(Loc.GetString("intro-character-creation-message-4"), _saveReminderMessage)
        {
            ClickAction = TutorialBubble.ClickBehaviour.Ignore,
            TippyVariant = TutorialBubble.Tippy.ClownRegular,
            MaxWidth = (chars?.PixelWidth ?? 375) - (chars?.GlobalPixelPosition.X ?? 0),
            ButtonsContainer =
            {
                Align = BoxContainer.AlignMode.Center,
                Orientation = BoxContainer.LayoutOrientation.Vertical,
            },
        };

        var continueButton = new Button
        {
            Text = Loc.GetString("intro-character-creation-click-to-continue-button"),
        };

        _charactersContainer = setup.FindControl<BoxContainer>("Characters");

        if (_charactersContainer != null)
        {
            _charactersContainer.OnChildAdded += OnCharacterButtonAdded;
            _charactersContainer.OnChildRemoved += OnCharacterButtonRemoved;
            SubscribeToCharacterButtons();
        }

        continueButton.OnPressed += _ =>
        {
            UnsubscribeFromCharacterButtons();
            overlay.DestroyOverlay();
            SixthOverlay();
        };

        bubble.ButtonsContainer.AddChild(continueButton);

        TutorialUi.PlanBubble(bubble, TutorialHighlightOverlay.OverlayControlPosition.BottomLeft, spacing: 20f, overlayId: name);
    }

    private void ProceedToSixth(BaseButton.ButtonEventArgs _)
    {
        UnsubscribeFromCharacterButtons();
        TutorialUi.ActiveOverlay?.DestroyOverlay();
        SixthOverlay();
    }

    private void SixthOverlay()
    {
        if (_lobby!.ProfileEditor is not { } editor)
        {
            CancelWithError("Cannot access the profile editor. Cancelling introduction.");
            return;
        }

        if (_lobby!.CharacterSetup is not { } setup)
        {
            CancelWithError("Cannot access the character setup window. Cancelling introduction.");
            return;
        }

        editor.EnableAllTabs();

        editor.OpenTab(2);

        editor.DisableAllTabsExcept(2);

        var chars = setup.FindControl<BoxContainer>("Characters");
        var name = $"{StepId}-6";
        var overlay = TutorialUi.PlanOverlay(name, default, Color.Transparent, false, false);

        var bubble = new TutorialBubble(Loc.GetString("intro-character-creation-message-5"), _saveReminderMessage)
        {
            ClickAction = TutorialBubble.ClickBehaviour.Ignore,
            TippyVariant = TutorialBubble.Tippy.None,
            MaxWidth = (chars?.PixelWidth ?? 375) - (chars?.GlobalPixelPosition.X ?? 0),
            ButtonsContainer =
            {
                Align = BoxContainer.AlignMode.Center,
                Orientation = BoxContainer.LayoutOrientation.Vertical,
            },
        };

        var continueButton = new Button
        {
            Text = Loc.GetString("intro-character-creation-click-to-continue-button"),
        };

        continueButton.OnPressed += _ =>
        {
            overlay.DestroyOverlay();
            SeventhOverlay();
        };

        bubble.ButtonsContainer.AddChild(continueButton);

        TutorialUi.PlanBubble(bubble, TutorialHighlightOverlay.OverlayControlPosition.BottomLeft, spacing: 20f, overlayId: name);
    }

    private void SeventhOverlay()
    {
        if (_lobby!.ProfileEditor is not { } editor)
        {
            CancelWithError("Cannot access the profile editor. Cancelling introduction.");
            return;
        }

        if (_lobby!.CharacterSetup is not { } setup)
        {
            CancelWithError("Cannot access the character setup window. Cancelling introduction.");
            return;
        }

        editor.EnableAllTabs();

        editor.OpenTab(3);

        editor.DisableAllTabsExcept(3);

        var chars = setup.FindControl<BoxContainer>("Characters");
        var name = $"{StepId}-7";
        var overlay = TutorialUi.PlanOverlay(name, default, Color.Transparent, false, false);

        var bubble = new TutorialBubble(Loc.GetString("intro-character-creation-message-6",
            ("humanoid-profile-editor-traits-tab", Loc.GetString("humanoid-profile-editor-traits-tab"))),
            _saveReminderMessage)
        {
            ClickAction = TutorialBubble.ClickBehaviour.Ignore,
            TippyVariant = TutorialBubble.Tippy.None,
            MaxWidth = (chars?.PixelWidth ?? 375) - (chars?.GlobalPixelPosition.X ?? 0),
            ButtonsContainer =
            {
                Align = BoxContainer.AlignMode.Center,
                Orientation = BoxContainer.LayoutOrientation.Vertical,
            },
        };

        var continueButton = new Button
        {
            Text = Loc.GetString("intro-character-creation-click-to-continue-button"),
        };

        continueButton.OnPressed += _ =>
        {
            overlay.DestroyOverlay();
            EighthOverlay();
        };

        bubble.ButtonsContainer.AddChild(continueButton);

        TutorialUi.PlanBubble(bubble, TutorialHighlightOverlay.OverlayControlPosition.BottomLeft, spacing: 20f, overlayId: name);
    }

    private void EighthOverlay()
    {
        if (_lobby!.ProfileEditor is not { } editor)
        {
            CancelWithError("Cannot access the profile editor. Cancelling introduction.");
            return;
        }
        if (_lobby!.CharacterSetup is not { } setup)
        {
            CancelWithError("Cannot access the character setup window. Cancelling introduction.");
            return;
        }


        editor.EnableAllTabs();

        editor.OpenTab(4);

        editor.DisableAllTabsExcept(4);

        var chars = setup.FindControl<BoxContainer>("Characters");
        var name = $"{StepId}-8";
        var overlay = TutorialUi.PlanOverlay(name, default, Color.Transparent, false, false);

        var bubble = new TutorialBubble(Loc.GetString("intro-character-creation-message-7",
            ("humanoid-profile-editor-markings-tab", Loc.GetString("humanoid-profile-editor-markings-tab"))),
            _saveReminderMessage)
        {
            ClickAction = TutorialBubble.ClickBehaviour.Ignore,
            TippyVariant = TutorialBubble.Tippy.None,
            MaxWidth = (chars?.PixelWidth ?? 375) - (chars?.GlobalPixelPosition.X ?? 0),
            ButtonsContainer =
            {
                Align = BoxContainer.AlignMode.Center,
                Orientation = BoxContainer.LayoutOrientation.Vertical,
            },
        };

        var continueButton = new Button
        {
            Text = Loc.GetString("intro-character-creation-click-to-continue-button"),
        };

        continueButton.OnPressed += _ =>
        {
            overlay.DestroyOverlay();
            NinthOverlay();
        };

        bubble.ButtonsContainer.AddChild(continueButton);

        TutorialUi.PlanBubble(bubble, TutorialHighlightOverlay.OverlayControlPosition.BottomLeft, spacing: 20f, overlayId: name);
    }

    private void NinthOverlay()
    {
        // CD records tab is gone on current lobby - skip to finale
        TenthOverlay();
    }

    private void TenthOverlay()
    {
        if (_lobby!.ProfileEditor is not { } editor)
        {
            CancelWithError("Cannot access the profile editor. Cancelling introduction.");
            return;
        }

        if (_lobby!.CharacterSetup is not { } setup)
        {
            CancelWithError("Cannot access the character setup window. Cancelling introduction.");
            return;
        }

        editor.EnableAllTabs();

        var chars = setup.FindControl<BoxContainer>("Characters");
        var name = $"{StepId}-10";
        var overlay = TutorialUi.PlanOverlay(name, editor.SpriteView, Color.Green);

        var bubble = new TutorialBubble(Loc.GetString("intro-character-creation-final-message"))
        {
            ClickAction = TutorialBubble.ClickBehaviour.Ignore,
            TippyVariant = TutorialBubble.Tippy.ClownRegular,
            MaxWidth = (chars?.PixelWidth ?? 375) - (chars?.GlobalPixelPosition.X ?? 0),
        };

        var continueButton = new Button
        {
            Text = Loc.GetString("intro-character-creation-click-to-continue-button"),
        };

        _gui!.CharacterSetupStateSwitched -= OnCharacterSetupStateSwitch;
        continueButton.OnPressed += _ =>
        {
            overlay.DestroyOverlay();
            EndStep();
        };

        bubble.ButtonsContainer.AddChild(continueButton);

        TutorialUi.PlanBubble(bubble, TutorialHighlightOverlay.OverlayControlPosition.CenterLeft, editor.SpriteView, spacing: 20f, overlayId: name);
    }

    private void EndStep()
    {
        if (_lobby!.ProfileEditor is not { } editor)
        {
            CancelWithError("Cannot access the profile editor. Cancelling introduction.");
            Tutorial.CancelTutorial();
            return;
        }

        editor.EnableAllTabs();
        Tutorial.NextStep();
    }

    private void OnCharacterSetupStateSwitch(bool entered, LobbyGui.LobbyGuiState state)
    {
        if (!entered)
            Tutorial.CancelTutorial();
    }
}
