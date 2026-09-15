using Content.Client._Polonium.Tutorial.Lobby.Steps;
using Content.Client._Polonium.Tutorial.Lobby.UI;
using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using TutorialPresentationSystem = Content.Client._Polonium.Tutorial.TutorialPresentationSystem;
using Content.Shared._Polonium.Tutorial.Lobby;
using Content.Shared.CCVar;
using Robust.Client;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Localization;

namespace Content.Client._Polonium.Tutorial.Lobby;

public sealed partial class TutorialManager : SharedTutorialLobbyManager
{
    // Dependencies
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IUserInterfaceManager _uiMan = default!;
    [Dependency] private IStateManager _stateMan = default!;
    [Dependency] private ILocalizationManager _loc = default!;
    [Dependency] private IGameController _game = default!;
    [Dependency] private IEntitySystemManager _systems = default!;
    [Dependency] private IBaseClient _client = default!;
    private ISawmill _sawmill = default!;
    private TutorialUIController _tutorialUi = default!;
    private LobbyUIController _lobby = default!;

    // Public Properties
    public ClientsideTutorialLobbyStep CurrentStep => ConvertToLegacyStep(ActiveStep);
    public bool IsTutorialActive => _currentStepIndex >= 0;
    public bool IsPaused => _isPaused;
    public bool IsCompleted => Progress.IsCompleted;

    private readonly List<IClientsideNavTutorialStep> _steps = new();
    private int _currentStepIndex = -1;
    private bool _isPaused = false;
    private bool? _dbCompleted;
    private bool _offerOpen;

    public IClientsideNavTutorialStep? ActiveStep =>
        _currentStepIndex >= 0 && _currentStepIndex < _steps.Count
            ? _steps[_currentStepIndex]
            : null;

    public TutorialLobbyProgress Progress { get; } = new();

    public int CurrentStepIndex => _currentStepIndex;
    public bool CanGoBack => _currentStepIndex > 0;
    public bool CanGoForward => _currentStepIndex < _steps.Count - 1;

    public event Action<IClientsideNavTutorialStep>? OnActiveStepChanged;
    public event Action<IClientsideNavTutorialStep>? OnActiveStepSkipped;
    public event Action? OnTutorialPaused;
    public event Action? OnTutorialResumed;
    public event Action? OnTutorialCompleted;
    public event Action? OnTutorialCancelled;


    public void Initialize()
    {
        _sawmill = Logger.GetSawmill("tutorial.lobby");
        _tutorialUi = _uiMan.GetUIController<TutorialUIController>();
        _lobby = _uiMan.GetUIController<LobbyUIController>();

        // Register steps
        RegisterSteps();

        OnActiveStepChanged += OnStepChanged;
        OnActiveStepSkipped += OnStepSkipped;
        OnTutorialPaused += OnPaused;
        OnTutorialResumed += OnResumed;
        OnTutorialCompleted += OnCompleted;
        OnTutorialCancelled += OnCancelled;

        _stateMan.OnStateChanged += OnStateChanged;
        _client.RunLevelChanged += OnRunLevelChanged;
    }

    #region Core Methods

    public void StartTutorial()
    {
        if (_cfg.GetCVar(CCVars.IntroServerMode) == IntroMode.Tutorial)
        {
            _systems.GetEntitySystem<TutorialPresentationSystem>().RequestPracticalJoin();
            return;
        }

        var training = _cfg.GetCVar(CCVars.IntroSolitaryServerConnectionString);
        if (!string.IsNullOrEmpty(training))
        {
            GoToTrainingServer();
            return;
        }

        if (IsTutorialActive)
        {
            _sawmill.Error(_loc.GetString("intro-begin-error-already-running"));
            return;
        }

        if (_stateMan.CurrentState is not LobbyState)
        {
            _sawmill.Error(_loc.GetString("intro-begin-error-outside-lobby"));
            return;
        }

        Progress.HasDeclined = false;

        if (IsPaused && IsTutorialActive)
            ResumeTutorial();
        else
            Start(null);
    }

    /// <summary>
    /// Starts the introduction from the beginning or resumes from saved position.
    /// </summary>
    public bool Start(int? fromStepIndex = null)
    {
        if (_steps.Count == 0)
            return false;

        Progress.IsCompleted = false;
        _currentStepIndex = fromStepIndex ?? (_cfg.GetCVar(CCVars.SkipLobbyIntroDebug) ? _steps.Count - 1 : 0);
        _isPaused = false;

        return ExecuteCurrentStep();
    }

    /// <summary>
    /// Pauses the current introduction sequence.
    /// </summary>
    public void PauseTutorial()
    {
        if (_isPaused || !IsTutorialActive)
            return;

        Progress.IsPaused = true;

        CleanupCurrentStep();
        _isPaused = true;
        OnTutorialPaused?.Invoke();
    }

    /// <summary>
    /// Resumes a paused introduction sequence.
    /// </summary>
    public bool ResumeTutorial()
    {
        if (!_isPaused || !IsTutorialActive)
            return false;

        Progress.IsPaused = false;
        _isPaused = false;
        OnTutorialResumed?.Invoke();

        // the lobby may look different than when we left, walk back to something that still fits
        if (!RewindToRunnableStep())
        {
            CancelTutorial();
            return false;
        }

        var step = ActiveStep;
        if (step is not null)
        {
            step.OnReenter();
            OnActiveStepChanged?.Invoke(step);
        }

        return true;
    }

    /// <summary>
    /// Walks backwards until a step reports it can run here. Used when the player closed a window
    /// or left the lobby and the step we were on no longer makes sense.
    /// </summary>
    private bool RewindToRunnableStep()
    {
        while (_currentStepIndex >= 0)
        {
            if (ActiveStep is { } step && step.CanExecute())
                return true;

            _currentStepIndex--;
        }

        return false;
    }

    /// <summary>Player asked to stop. Drop the flow but leave a hint about the lobby button.</summary>
    public void SkipTutorial()
    {
        _cfg.SetCVar(CCVars.IntroDeclined, true);
        _cfg.SaveToFile();
        Progress.HasDeclined = true;
        CancelTutorial();
        ShowRestartHint();
    }

    public void OnDbCompletion(bool completed)
    {
        _dbCompleted = completed;
        if (completed)
            MarkCompleted();

        TryOfferTraining();
    }

    public void MarkCompleted()
    {
        _dbCompleted = true;

        // the finale bubble calls this on every redraw, and a redial right after has to find it on disk
        if (_cfg.GetCVar(CCVars.IntroCompleted))
            return;

        _cfg.SetCVar(CCVars.IntroCompleted, true);
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
        _tutorialUi.PlanOverlay(id, button, Color.FromHex("#65B8E2"), highlightMargin: 4f);

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

    /// <summary>
    /// Moves to the next introduction step.
    /// </summary>
    public bool NextStep()
    {
        if (_isPaused)
            return false;

        if (_currentStepIndex >= _steps.Count - 1)
        {
            CompleteTutorial();
            return false;
        }

        CleanupCurrentStep();
        _currentStepIndex++;

        return ExecuteCurrentStep();
    }

    /// <summary>
    /// Returns to the previous introduction step.
    /// </summary>
    public bool PreviousStep()
    {
        if (_currentStepIndex <= 0)
            return false;

        CleanupCurrentStep();
        _currentStepIndex--;

        var step = _steps[_currentStepIndex];
        step.OnReenter();
        OnActiveStepChanged?.Invoke(step);

        return true;
    }

    /// <summary>
    /// Cancels the introduction sequence.
    /// </summary>
    public void CancelTutorial()
    {
        CleanupCurrentStep();
        _currentStepIndex = -1;
        Progress.IsCompleted = false;
        Progress.IsPaused = false;
        Progress.CurrentStepId = string.Empty;
        _isPaused = false;
        OnTutorialCancelled?.Invoke();
    }

    public void CompleteTutorial() // TODO: może ProceedTutorial — gracz przechodzi dalej na serwer treningowy.
    {
        CleanupCurrentStep();
        _currentStepIndex = -1;
        Progress.IsCompleted = true;
        Progress.IsPaused = false;
        Progress.CurrentStepId = string.Empty;
        _isPaused = false;
        OnTutorialCompleted?.Invoke();
    }

    public bool GoToStep(int stepIndex)
    {
        if (stepIndex < 0 || stepIndex >= _steps.Count)
            return false;

        CleanupCurrentStep();
        _currentStepIndex = stepIndex;

        return ExecuteCurrentStep();
    }

    public bool GoToStep(string stepId)
    {
        var index = _steps.FindIndex(s => s.StepId == stepId);
        return index >= 0 && GoToStep(index);
    }

    private bool ExecuteCurrentStep()
    {
        var maxAttempts = _steps.Count - _currentStepIndex;
        var attempts = 0;

        while (attempts < maxAttempts)
        {
            var step = ActiveStep;
            if (step is null)
                return false;

            if (!step.CanExecute())
            {
                _sawmill.Info($"Step '{step.StepId}' cannot be executed, skipping...");
                OnActiveStepSkipped?.Invoke(step);

                if (_currentStepIndex >= _steps.Count - 1)
                {
                    CompleteTutorial();
                    return false;
                }

                _currentStepIndex++;
                attempts++;
                continue;
            }

            Progress.CurrentStepId = step.StepId;

            var success = step.Execute();
            if (!success)
            {
                _sawmill.Warning($"Step '{step.StepId}' execution failed, falling back");

                // do not strand the player on a step that drew nothing
                _currentStepIndex--;
                if (RewindToRunnableStep() && ActiveStep is { } fallback && fallback.Execute())
                {
                    Progress.CurrentStepId = fallback.StepId;
                    OnActiveStepChanged?.Invoke(fallback);
                    return true;
                }

                CancelTutorial();
                ShowRestartHint();
                return false;
            }

            OnActiveStepChanged?.Invoke(step);
            return true;
        }

        _sawmill.Warning("All remaining steps cannot be executed, completing tutorial lobby flow");
        CompleteTutorial();
        return false;
    }

    private void CleanupCurrentStep()
    {
        ActiveStep?.Cleanup();
    }

    /// <summary>
    /// Resets the tutorial lobby sequence to its initial state.
    /// </summary>
    private bool Reset()
    {
        var currentOverlay = _tutorialUi.ActiveOverlay;
        if (currentOverlay is not null && currentOverlay.Id != TutorialPresentationSystem.OverlayId)
            _tutorialUi.RemoveOverlay(currentOverlay);

        _tutorialUi.ClearPendingOverlays();
        _tutorialUi.ClearPendingBubbles();

        return false;
    }

    #endregion

    #region Helpers

    private bool ShouldOfferTraining()
    {
#if DEBUG
        if (!_cfg.GetCVar(CCVars.IntroInDebug))
            return false;
#endif

        if (_cfg.GetCVar(CCVars.IntroServerMode) != IntroMode.Main)
            return false;

        if (_cfg.GetCVar(CCVars.IntroDeclined) || _cfg.GetCVar(CCVars.IntroCompleted))
            return false;

        if (_dbCompleted == true)
            return false;

        if (string.IsNullOrEmpty(_cfg.GetCVar(CCVars.IntroSolitaryServerConnectionString)))
            return false;

        return true;
    }

    private void GoToTrainingServer()
    {
        var address = _cfg.GetCVar(CCVars.IntroSolitaryServerConnectionString);
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

    #endregion

    #region Step Registration

    /// <summary>
    /// Registers the initial set of tutorial lobby steps in order.
    /// </summary>
    private void RegisterSteps()
    {
        _steps.Add(new WelcomeStep());
        _steps.Add(new LobbyOverviewStep());
        _steps.Add(new CharacterCreationStep());
        _steps.Add(new ProceedPromptStep());
    }

    #endregion

    #region Event Handlers

    private void OnStepChanged(IClientsideNavTutorialStep step)
    {
        _sawmill.Debug($"Tutorial lobby step changed to: {step.StepId}");
    }

    private void OnPaused()
    {
        _sawmill.Debug("Tutorial lobby paused");
    }

    private void OnResumed()
    {
        _sawmill.Debug("Tutorial lobby resumed");
    }

    private void OnCompleted()
    {
        _sawmill.Info(_loc.GetString("intro-info-complete"));
        Reset();
    }

    private void OnCancelled()
    {
        _sawmill.Debug("Tutorial lobby has been cancelled");
        Reset();
    }

    private void OnLobbyEntered(LobbyGui gui)
    {
#if DEBUG
        if (!_cfg.GetCVar(CCVars.IntroInDebug))
            return;
#endif

        if (gui.LinksBanner.TutorialButton is { } button)
        {
            button.Disabled = false;
        }

        // a cancelled or crashed run can leave the editor half locked, always undo that here
        _lobby.ProfileEditor?.EnableAllTabs();

        if (_cfg.GetCVar(CCVars.IntroServerMode) == IntroMode.Tutorial)
        {
            _systems.GetEntitySystem<TutorialPresentationSystem>().RequestPracticalJoin();
            return;
        }

        // a finished or refused run is not offered again, the lobby button is still there
        if (Progress.IsCompleted || Progress.HasDeclined)
            return;

        if (IsPaused && IsTutorialActive)
        {
            ResumeTutorial();
            return;
        }

        if (IsTutorialActive)
            return;

        TryOfferTraining();
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
        };

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

    private void OnStateChanged(StateChangedEventArgs args)
    {
        if (args.NewState is LobbyState { Lobby: { } gui })
        {
            OnLobbyEntered(gui);
            return;
        }

        CloseTrainingOffer();

        if (IsTutorialActive)
            PauseTutorial();
    }

    private void OnRunLevelChanged(object? sender, RunLevelChangedEventArgs args)
    {
        if (args.NewLevel != ClientRunLevel.Initialize)
            return;

        _dbCompleted = null;
        CloseTrainingOffer();
    }

    private void OnStepSkipped(IClientsideNavTutorialStep step)
    {
        _sawmill.Debug($"Tutorial lobby step '{step.StepId}' was skipped due to CanExecute() returning false");
    }

    #endregion
}
