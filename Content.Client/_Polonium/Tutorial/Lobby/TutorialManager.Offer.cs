using Content.Client._Polonium.Tutorial.Lobby.UI;
using Content.Client.Lobby;
using Content.Client.Resources;
using TutorialPresentationSystem = Content.Client._Polonium.Tutorial.TutorialPresentationSystem;
using Content.Shared._Polonium.Tutorial;
using Content.Shared.CCVar;
using Robust.Client;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;

namespace Content.Client._Polonium.Tutorial.Lobby;

/// <summary>
/// Offering the training server to someone who has not been through it, and remembering the
/// answer. Nobody is asked twice in one session, and never while something else is open.
/// </summary>
public sealed partial class TutorialManager
{
    [Dependency] private IGameController _game = default!;
    [Dependency] private IResourceCache _resCache = default!;
    private bool _offerOpen;

    public void OnDbCompletion(bool completed)
    {
        _dbCompleted = completed;
        if (completed)
            MarkCompleted();

        if (_hopWindow is { Disposed: false })
            _hopWindow.SetCompleted(HasCompletedTraining());

        TryOfferTraining();
    }

    public void OpenTrainingHopWindow()
    {
        if (GetIntroMode() != SharedTutorialSystem.IntroMain)
            return;

        if (string.IsNullOrEmpty(_cfg.GetCVar(CCVars.TutorialSolitaryServerConnectionString)))
            return;

        CloseTrainingOffer();

        if (_hopWindow == null || _hopWindow.Disposed)
        {
            _hopWindow = new TutorialHopWindow();
            _hopWindow.OnConfirm += GoToTrainingServer;
        }

        _hopWindow.SetCompleted(HasCompletedTraining());
        _hopWindow.OpenCentered();
    }

    public void MarkCompleted()
    {
        _dbCompleted = true;

        // the finale bubble calls this on every redraw, and a redial right after has to find it on disk
        if (_cfg.GetCVar(CCVars.TutorialCompleted))
            return;

        _cfg.SetCVar(CCVars.TutorialCompleted, true);
        _cfg.SaveToFile();
    }

    /// <summary>
    /// Points at the lobby button so a player who bailed out knows how to come back.
    /// </summary>
    public void ShowRestartHint()
    {
        if (_stateMan.CurrentState is not LobbyState { Lobby: { } lobby })
            return;

        if (lobby.LinksBanner.TutorialButton is not { } button)
            return;

        const string id = "tutorial-restart-hint";
        _tutorialUi.PlanOverlay(id, button, Color.FromHex("#65B8E2"), highlightMargin: 4f, orphanOnHighlightClick: true);

        if (_tutorialUi.ActiveOverlay?.Id != id)
            return;

        _tutorialUi.PlanBubble(
            new TutorialBubble(_loc.GetString("intro-welcome-reminder-message",
                ("server-info-introduction-button", button.Text ?? string.Empty)))
            {
                ClickAction = TutorialBubble.ClickBehaviour.CloseOverlay,
                TippyVariant = TutorialBubble.Tippy.ClownPointing,
            },
            TutorialHighlightOverlay.OverlayControlPosition.BottomRight,
            relativeToControl: button,
            overlayId: id);
    }

    public void ShowPracticalLaterHint()
    {
        if (_stateMan.CurrentState is not LobbyState { Lobby: { } lobby })
            return;

        var button = lobby.ReadyButton;
        const string id = "tutorial-practical-later";
        _tutorialUi.PlanOverlay(id, button, Color.FromHex("#65B8E2"), highlightMargin: 4f, orphanOnHighlightClick: true);

        if (_tutorialUi.ActiveOverlay?.Id != id)
            return;

        _tutorialUi.PlanBubble(
            new TutorialBubble(_loc.GetString("intro-lobby-skip-later",
                ("lobby-join-button", button.Text ?? string.Empty)))
            {
                ClickAction = TutorialBubble.ClickBehaviour.CloseOverlay,
                TippyVariant = TutorialBubble.Tippy.ClownPointing,
            },
            TutorialHighlightOverlay.OverlayControlPosition.BottomRight,
            relativeToControl: button,
            overlayId: id);
    }

    private void CloseTrainingHopWindow()
    {
        _hopWindow?.Close();
    }

    private bool ShouldOfferTraining()
    {
        if (GetIntroMode() != SharedTutorialSystem.IntroMain)
            return false;

        if (_cfg.GetCVar(CCVars.TutorialDeclined) || _cfg.GetCVar(CCVars.TutorialCompleted))
            return false;

        if (_dbCompleted == true)
            return false;

        if (string.IsNullOrEmpty(_cfg.GetCVar(CCVars.TutorialSolitaryServerConnectionString)))
            return false;

        return true;
    }

    private void GoToTrainingServer()
    {
        var address = _cfg.GetCVar(CCVars.TutorialSolitaryServerConnectionString);
        if (string.IsNullOrEmpty(address))
            return;

        try
        {
            _game.Redial(address, _loc.GetString("intro-solitary-server-hopping-message"));
        }
        catch (Exception e)
        {
            _sawmill.Warning($"Training server redial failed: {e.Message}");
        }
    }

    private void TryOfferTraining()
    {
        if (_offerOpen || IsTutorialActive)
            return;

        if (_stateMan.CurrentState is not LobbyState)
            return;

        if (_dbCompleted is null)
            return;

        if (!ShouldOfferTraining())
            return;

        ShowTrainingOffer();
    }

    private void ShowTrainingOffer()
    {
        const string id = "training-offer";
        _offerOpen = true;
        _tutorialUi.PlanOverlay(id);

        var bubble = new TutorialBubble(
            _loc.GetString("intro-training-offer-message-1"),
            _loc.GetString("intro-training-offer-message-2"))
        {
            ClickAction = TutorialBubble.ClickBehaviour.Ignore,
            TippyVariant = TutorialBubble.Tippy.WavingHand,
            FullSize = true,
        };

        bubble.ContentContainer.AddChild(new TextureRect
        {
            Texture = _resCache.GetTexture("/Textures/_Polonium/Interface/Misc/intro_markers/Text/greeting_text.png"),
            Stretch = TextureRect.StretchMode.Scale,
            HorizontalAlignment = Control.HAlignment.Center,
        });

        var agree = TutorialBubble.MakeButton(_loc.GetString("intro-training-offer-agree"));
        var decline = TutorialBubble.MakeButton(_loc.GetString("intro-training-offer-disagree"), primary: false);

        var column = new BoxContainer
        {
            Align = BoxContainer.AlignMode.Center,
            Orientation = BoxContainer.LayoutOrientation.Vertical,
        };
        column.AddChild(agree);
        column.AddChild(decline);
        bubble.ButtonsContainer.AddChild(column);

        agree.OnPressed += _ =>
        {
            CloseTrainingOffer();
            GoToTrainingServer();
        };

        decline.OnPressed += _ =>
        {
            CloseTrainingOffer();
            SkipTutorial();
        };

        _tutorialUi.PlanBubble(bubble, TutorialHighlightOverlay.OverlayControlPosition.Center, overlayId: id);
    }

    private void CloseTrainingOffer()
    {
        if (!_offerOpen)
            return;

        _offerOpen = false;
        if (_tutorialUi.ActiveOverlay?.Id == "training-offer")
            _tutorialUi.RequestClose(false);
    }
}
