// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client._Polonium.Tutorial.Lobby.Steps;
using Content.Client._Polonium.Tutorial.Lobby.UI;
using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using Content.Client.Players.PlayTimeTracking;
using Content.Shared._Polonium.Tutorial.Lobby;
using Content.Shared.CCVar;
using Content.Shared.Players;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Localization;
using Robust.Shared.Network;

namespace Content.Client._Polonium.Tutorial.Lobby;

public sealed class TutorialManager : SharedTutorialLobbyManager
{
    // Dependencies
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IUserInterfaceManager _uiMan = default!;
    [Dependency] private readonly IStateManager _stateMan = default!;
    [Dependency] private readonly IResourceCache _resCache = default!;
    [Dependency] private readonly ILocalizationManager _loc = default!;
    [Dependency] private readonly JobRequirementsManager _jobReqMan = default!;
    [Dependency] private readonly IPlayerManager _playerMan = default!;
    private ISawmill _sawmill = default!;
    private TutorialUIController _tutorialUi = default!;
    private LobbyUIController _lobby = default!;

    // Public Properties
    public ClientsideTutorialLobbyStep CurrentStep => ConvertToLegacyStep(ActiveStep);
    public bool IsTutorialActive => _currentStepIndex >= 0;
    public bool IsPaused => _isPaused;

    private readonly List<IClientsideNavTutorialStep> _steps = new();
    private int _currentStepIndex = -1;
    private bool _isPaused = false;

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
    }

    #region Core Methods

    public void StartTutorial()
    {
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

        if (Progress.IsPaused)
        {
            ResumeTutorial();
        }
        else
        {
            Start(null);
        }
    }

    /// <summary>
    /// Starts the introduction from the beginning or resumes from saved position.
    /// </summary>
    public bool Start(int? fromStepIndex = null)
    {
        if (_steps.Count == 0)
            return false;

        Progress.IsCompleted = false;
        _currentStepIndex = fromStepIndex ?? 0;
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

        var step = ActiveStep;
        if (step is not null)
        {
            step.OnReenter();
            OnActiveStepChanged?.Invoke(step);
        }

        return true;
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
                _sawmill.Warning($"Step '{step.StepId}' execution failed");
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
        if (currentOverlay is not null)
            _tutorialUi.RemoveOverlay(currentOverlay);

        _tutorialUi.ClearPendingOverlays();
        _tutorialUi.ClearPendingBubbles();

        return false;
    }

    #endregion

    #region Helpers

    public bool CheckEligibility(out TimeSpan? overall)
    {
        overall = null;
        if (!_cfg.GetCVar(CCVars.IntroEnabled))
            return false;
        overall = _jobReqMan.FetchOverallPlaytime();
        return overall <= TimeSpan.FromMinutes(_cfg.GetCVar(CCVars.IntroMaxPlaytime));
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

        _lobby.ProfileEditor?.EnableAllTabs();

        if (!CheckEligibility(out var overall))
            return;

        _sawmill.Debug($"Player is eligible for tutorial lobby. Playtime: {(int) overall!.Value.TotalMinutes} mins.");

        StartTutorial();
    }

    private void OnStateChanged(StateChangedEventArgs args)
    {
        if (args.NewState is LobbyState { Lobby: { } gui })
        {
            OnLobbyEntered(gui);
        }
        else
        //if (IsTutorialActive)
        {
            CancelTutorial();
        }
    }

    private void OnStepSkipped(IClientsideNavTutorialStep step)
    {
        _sawmill.Debug($"Tutorial lobby step '{step.StepId}' was skipped due to CanExecute() returning false");
    }

    #endregion
}
