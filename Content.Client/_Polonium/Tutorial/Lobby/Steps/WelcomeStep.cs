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
            Loc.GetString("intro-welcome-message-2"))
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

        var agreeButton = TutorialBubble.MakeButton(Loc.GetString("intro-welcome-begin-agree-button"));

        var disagreeButton = TutorialBubble.MakeButton(Loc.GetString("intro-welcome-begin-disagree-button"), primary: false);

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
        disagreeButton.OnPressed += _ => Decline();

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

    /// <summary>
    /// A no here has to stick for the session, otherwise the lobby asks again every time the
    /// player comes back from a round.
    /// </summary>
    private void Decline()
    {
        Cleanup();
        Tutorial.SkipTutorial();
    }
}
