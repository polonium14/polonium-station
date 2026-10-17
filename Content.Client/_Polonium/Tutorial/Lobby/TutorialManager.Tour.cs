using Content.Client.Lobby;
using TutorialPresentationSystem = Content.Client._Polonium.Tutorial.TutorialPresentationSystem;
using Content.Shared._Polonium.Tutorial;
using Content.Shared._Polonium.Tutorial.Lobby;
using Content.Shared.CCVar;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;

namespace Content.Client._Polonium.Tutorial.Lobby;

/// <summary>
/// Walking the tour. A step is torn down before the next one is set up, and a step whose
/// control has since left the screen is rewound past rather than left hanging.
/// </summary>
public sealed partial class TutorialManager
{
    [Dependency] private IEntitySystemManager _systems = default!;

    public void StartTutorial()
    {
        var mode = GetIntroMode();
        if (mode == SharedTutorialSystem.IntroNone)
            return;

        if (mode == SharedTutorialSystem.IntroTutorial)
        {
            if (IsPaused && IsTutorialActive)
                ResumeTutorial();
            else if (!IsTutorialActive)
                Start(null);
            return;
        }

        if (mode == SharedTutorialSystem.IntroMain)
            OpenTrainingHopWindow();
    }

    /// <summary>
    /// Starts the introduction from the beginning or resumes from saved position.
    /// </summary>
    public bool Start(int? fromStepIndex = null)
    {
        if (GetIntroMode() != SharedTutorialSystem.IntroTutorial)
            return false;

        if (_info.IsRulesPopupOpen)
            return false;

        if (_steps.Count == 0)
            return false;

        Progress.IsCompleted = false;
        _currentStepIndex = fromStepIndex ?? (_cfg.GetCVar(CCVars.TutorialSkipLobbyDebug) ? _steps.Count - 1 : 0);
        _isPaused = false;

        var ok = ExecuteCurrentStep();
        NotifyLobbyTour(IsTutorialActive);
        return ok;
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
    /// The tour counts as running but nothing of it is on screen. Put the current step back
    /// instead of leaving the player with a lobby that silently ignores them.
    /// </summary>
    public void EnsureTourVisible()
    {
        if (!IsTutorialActive || _info.IsRulesPopupOpen || _stateMan.CurrentState is not LobbyState)
            return;

        if (_isPaused)
        {
            ResumeTutorial();
            return;
        }

        if (_tutorialUi.ActiveOverlay != null)
            return;

        if (!RewindToRunnableStep())
        {
            CancelTutorial();
            return;
        }

        ActiveStep?.OnReenter();
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
        _cfg.SetCVar(CCVars.TutorialDeclined, true);
        _cfg.SaveToFile();
        Progress.HasDeclined = true;
        CancelTutorial();
        ShowRestartHint();
    }

    public void SkipLobbyTour()
    {
        CompleteTutorial();
        ShowPracticalLaterHint();
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
        NotifyLobbyTour(false);
        OnTutorialCancelled?.Invoke();
    }

    public void CompleteTutorial()
    {
        CleanupCurrentStep();
        _currentStepIndex = -1;
        Progress.IsCompleted = true;
        Progress.IsPaused = false;
        Progress.CurrentStepId = string.Empty;
        _isPaused = false;
        NotifyLobbyTour(false);
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

    private void NotifyLobbyTour(bool active)
    {
        if (_lobbyTourSent == active)
            return;

        if (!_systems.TryGetEntitySystem(out TutorialPresentationSystem? tutorial))
            return;

        _lobbyTourSent = active;
        tutorial.SetLobbyTourActive(active);
    }

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

    private void OnStepSkipped(IClientsideNavTutorialStep step)
    {
        _sawmill.Debug($"Tutorial lobby step '{step.StepId}' was skipped due to CanExecute() returning false");
    }
}
