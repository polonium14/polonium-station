// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client._Polonium.Tutorial.Lobby.UI;
using Content.Client.Lobby;
using Robust.Client.ResourceManagement;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using System.Numerics;
using Content.Client.Resources;
using Content.Shared._Polonium.Tutorial.Lobby;

namespace Content.Client._Polonium.Tutorial.Lobby.Steps;

public sealed class WelcomeStep : ClientsideNavTutorialStep
{
    public override string StepId => "welcome";

    public override bool Execute()
    {
        if (StateMan.CurrentState is not LobbyState { Lobby: { } lobby })
            return false;

        // 1. Welcome message with header
        TutorialUi.PlanOverlay(StepId);
        var welcomeBubble = new TutorialBubble(
            Loc.GetString("intro-welcome-message-1"),
            Loc.GetString("intro-welcome-message-2"),
            Loc.GetString("intro-welcome-message-3"))
        {
            ClickAction = TutorialBubble.ClickBehaviour.Ignore,
            TippyVariant = TutorialBubble.Tippy.WavingHand,
        };
        TutorialUi.PlanBubble(welcomeBubble, TutorialHighlightOverlay.OverlayControlPosition.Center);

        var helloText = new TextureRect
        {
            Texture = ResCache.GetTexture("/Textures/_Polonium/Interface/Misc/intro_markers/Text/greeting_text.png"),
            //SetSize = new Vector2(256, 96),
            Stretch = TextureRect.StretchMode.Scale,
            HorizontalAlignment = Control.HAlignment.Center,
        };

        var questionLabel = new RichTextLabel
        {
            Margin = new Thickness(0f, 5f),
            Text = Loc.GetString("intro-welcome-begin-question-message"),
            ModulateSelfOverride = Color.Black,
            HorizontalAlignment = Control.HAlignment.Center,
        };

        var agreeButton = new Button
        {
            Text = Loc.GetString("intro-welcome-begin-agree-button"),
            HorizontalAlignment = Control.HAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0),
        };

        var disagreeButton = new Button
        {
            Text = Loc.GetString("intro-welcome-begin-disagree-button"),
            HorizontalAlignment = Control.HAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0),
        };

        var buttonContainer = new BoxContainer
        {
            Align = BoxContainer.AlignMode.Center,
            Orientation = BoxContainer.LayoutOrientation.Vertical,
        };

        welcomeBubble.ContentContainer.AddChild(helloText);
        buttonContainer.AddChild(questionLabel);
        buttonContainer.AddChild(agreeButton);
        buttonContainer.AddChild(disagreeButton);
        welcomeBubble.ButtonsContainer.AddChild(buttonContainer);

        agreeButton.OnPressed += _ => Tutorial.NextStep();
        disagreeButton.OnPressed += _ => CreateReminderOverlay();

        return true;
    }

    public override void OnReenter()
    {
        Execute();
    }

    public override bool CanExecute()
    {
        return StateMan.CurrentState is LobbyState;
    }

    private void CreateReminderOverlay()
    {
        Cleanup();

        if (StateMan.CurrentState is not LobbyState { Lobby: { } lobby })
            return;

        var button = lobby.LinksBanner.TutorialButton;

        if (button is null)
            return;

        var id = $"{StepId}-reminder";
        TutorialUi.PlanOverlay(id, button, Color.Green, isSelfClosingOnClick: false);

        // As all intro code is synchronous, we can be sure we are actually working with THIS overlay.
        // but as I am paranoid, let's perform a check anyway.
        if (TutorialUi.ActiveOverlay?.Id != id)
            return;

        button.Disabled = true;

        TutorialUi.PlanBubble(
            new TutorialBubble(Loc.GetString("intro-welcome-reminder-message", ("server-info-introduction-button", button.Text!)))
            {
                ClickAction = TutorialBubble.ClickBehaviour.FinishTutorial,
                TippyVariant = TutorialBubble.Tippy.ClownPointing,
            },
            TutorialHighlightOverlay.OverlayControlPosition.BottomRight,
            relativeToControl: button);

        TutorialUi.ActiveOverlay!.InternalOverlayClosedEvent += () => button.Disabled = false;
    }
}
