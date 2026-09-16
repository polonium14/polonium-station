using Content.Client._Polonium.Tutorial.Lobby.UI;
using Content.Client.Lobby;
using Robust.Client.ResourceManagement;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Content.Client.Resources;

namespace Content.Client._Polonium.Tutorial.Lobby.Steps;

public sealed class WelcomeStep : ClientsideNavTutorialStep
{
    public override string StepId => "welcome";

    public override bool Execute()
    {
        if (StateMan.CurrentState is not LobbyState)
            return false;

        var overlay = TutorialUi.PlanOverlay(StepId);
        var welcomeBubble = new TutorialBubble(
            Loc.GetString("intro-welcome-message-1"),
            Loc.GetString("intro-welcome-message-2"))
        {
            ClickAction = TutorialBubble.ClickBehaviour.CloseOverlay,
            TippyVariant = TutorialBubble.Tippy.WavingHand,
            FullSize = true,
        };

        var helloText = new TextureRect
        {
            Texture = ResCache.GetTexture("/Textures/_Polonium/Interface/Misc/intro_markers/Text/greeting_text.png"),
            Stretch = TextureRect.StretchMode.Scale,
            HorizontalAlignment = Control.HAlignment.Center,
        };

        welcomeBubble.ContentContainer.AddChild(helloText);
        TutorialUi.PlanBubble(welcomeBubble, TutorialHighlightOverlay.OverlayControlPosition.Center);

        overlay.InternalOverlayClosedEvent += () =>
        {
            if (Tutorial.IsTutorialActive && Tutorial.ActiveStep?.StepId == StepId)
                Tutorial.NextStep();
        };

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
}
