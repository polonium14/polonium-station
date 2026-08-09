// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client._Polonium.Tutorial.Lobby.UI;
using Content.Client.Lobby;
using Content.Shared.CCVar;
using Robust.Client;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;

namespace Content.Client._Polonium.Tutorial.Lobby.Steps;

public sealed class ProceedPromptStep : ClientsideNavTutorialStep
{
    [Dependency] private readonly IGameController _game = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public override string StepId => "proceed_prompt";
    public override bool CanExecute()
    {
        return StateMan.CurrentState is LobbyState;
    }

    public override bool Execute()
    {
        if (!string.IsNullOrEmpty(_cfg.GetCVar(CCVars.IntroSolitaryServerConnectionString)))
            PromptOverlay();
        else
            FallbackOverlay();

        return true;
    }

    public void FallbackOverlay()
    {
        var name = $"{StepId}-fallback";
        TutorialUi.PlanOverlay(name);

        var bubble = new TutorialBubble(Loc.GetString("intro-proceed-prompt-message-fallback"))
        {
            ClickAction = TutorialBubble.ClickBehaviour.CloseOverlay,
            TippyVariant = TutorialBubble.Tippy.None,
        };

        TutorialUi.PlanBubble(bubble, TutorialHighlightOverlay.OverlayControlPosition.Center, overlayId: name);
    }

    private void PromptOverlay()
    {
        TutorialUi.PlanOverlay($"{StepId}-1");

        var proceedBubble = new TutorialBubble(
            Loc.GetString("intro-proceed-prompt-message-1"))
        {
            ClickAction = TutorialBubble.ClickBehaviour.Ignore,
            TippyVariant = TutorialBubble.Tippy.None,
        };
        TutorialUi.PlanBubble(proceedBubble, TutorialHighlightOverlay.OverlayControlPosition.Center);

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

        buttonContainer.AddChild(questionLabel);
        buttonContainer.AddChild(agreeButton);
        buttonContainer.AddChild(disagreeButton);
        proceedBubble.ButtonsContainer.AddChild(buttonContainer);

        agreeButton.OnPressed += _ => OnAgree();
        disagreeButton.OnPressed += _ => Tutorial.NextStep();

        TutorialUi.PlanBubble(proceedBubble, TutorialHighlightOverlay.OverlayControlPosition.Center);
    }

    private void OnAgree()
    {
        Tutorial.CompleteTutorial(); // TODO: na tym momencie wprowadzenie do lobby się normalnie kończy, należy zapisać w bazie, że gracz doszedł do tego momentu

        _game.Redial(_cfg.GetCVar(CCVars.IntroSolitaryServerConnectionString),
            Loc.GetString("intro-solitary-server-hopping-message"));
    }
}
