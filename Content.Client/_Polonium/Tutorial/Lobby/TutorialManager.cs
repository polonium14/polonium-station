using Content.Client._Polonium.Tutorial.Lobby.Steps;
using Content.Client._Polonium.Tutorial.Lobby.UI;
using Content.Client.Lobby;
using Content.Client.UserInterface.Systems.Info;
using TutorialPresentationSystem = Content.Client._Polonium.Tutorial.TutorialPresentationSystem;
using Content.Shared._Polonium.Tutorial;
using Content.Shared._Polonium.Tutorial.Lobby;
using Content.Shared.CCVar;
using Robust.Client;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Localization;

namespace Content.Client._Polonium.Tutorial.Lobby;

/// <summary>
/// Holds the lobby tour: which step is showing, whether it is paused and who wants to be told
/// when that changes. The steps themselves are registered once and never mutated.
/// </summary>
public sealed partial class TutorialManager : SharedTutorialLobbyManager
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IUserInterfaceManager _uiMan = default!;
    [Dependency] private IStateManager _stateMan = default!;
    [Dependency] private ILocalizationManager _loc = default!;
    [Dependency] private IBaseClient _client = default!;
    private ISawmill _sawmill = default!;
    private TutorialUIController _tutorialUi = default!;
    private LobbyUIController _lobby = default!;
    private InfoUIController _info = default!;

    public bool IsTutorialActive => _currentStepIndex >= 0;
    public bool IsPaused => _isPaused;
    public bool IsCompleted => Progress.IsCompleted;

    private readonly List<IClientsideNavTutorialStep> _steps = new();
    private int _currentStepIndex = -1;
    private bool _isPaused = false;
    private bool? _dbCompleted;
    private bool _lobbyTourSent;
    private TutorialHopWindow? _hopWindow;

    public IClientsideNavTutorialStep? ActiveStep =>
        _currentStepIndex >= 0 && _currentStepIndex < _steps.Count
            ? _steps[_currentStepIndex]
            : null;

    public TutorialLobbyProgress Progress { get; } = new();

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
        _info = _uiMan.GetUIController<InfoUIController>();
        _info.RulesPopupChanged += OnRulesPopupChanged;

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

    public string GetIntroMode() =>
        SharedTutorialSystem.ReadIntroMode(_cfg, _sawmill ?? Logger.GetSawmill("tutorial.lobby"));

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

    private bool HasCompletedTraining()
    {
        return _dbCompleted == true || _cfg.GetCVar(CCVars.TutorialCompleted);
    }

    /// <summary>
    /// Registers the initial set of tutorial lobby steps in order.
    /// </summary>
    private void RegisterSteps()
    {
        _steps.Add(new BubbleSizeStep());
        _steps.Add(new WelcomeStep());
        _steps.Add(new LobbyOverviewStep());
        _steps.Add(new CharacterCreationStep());
        _steps.Add(new ProceedPromptStep());
    }
}
