using Content.Shared._Polonium.Tutorial.Lobby;
using Robust.Client.ResourceManagement;
using Robust.Client.State;
using Robust.Client.UserInterface;

namespace Content.Client._Polonium.Tutorial.Lobby.Steps;

/// <summary>
/// Represents a single step in an tutorial lobby sequence, providing the core logic and state for execution
/// and management.
/// </summary>
/// <remarks>This abstract base class defines the contract for tutorial lobby steps. You have to register new step using <see cref="TutorialNavigator"/> in order to make it active.</remarks>
public abstract partial class ClientsideNavTutorialStep : IClientsideNavTutorialStep
{
    [Dependency] protected IStateManager StateMan = default!;
    [Dependency] protected IUserInterfaceManager UiMan = default!;
    [Dependency] protected ILocalizationManager Loc = default!;
    [Dependency] protected IResourceCache ResCache = default!;
    [Dependency] protected TutorialManager Tutorial = default!;
    [Dependency] private ILogManager _logMan = default!;
    protected readonly TutorialUIController TutorialUi;
    protected readonly ISawmill Log;

    public abstract string StepId { get; }

    protected ClientsideNavTutorialStep()
    {
        IoCManager.InjectDependencies(this);
        TutorialUi = UiMan.GetUIController<TutorialUIController>();
        Log = _logMan.GetSawmill("tutorial.step");
    }

    public abstract bool Execute(); // TODO: fallback z cofnięciem wprowadzenia?

    public abstract bool CanExecute();

    public virtual void Cleanup()
    {
        TutorialUi.ClearPendingOverlays();
        TutorialUi.ClearPendingBubbles();

        if (TutorialUi.ActiveOverlay is not null)
            TutorialUi.RequestClose(false);
    }

    public virtual void OnReenter()
    {
        Execute();
    }

    protected void CancelWithError(string errorMessage)
    {
        Log.Error($"[{StepId}] {errorMessage}");
        Tutorial.CancelTutorial();
        Tutorial.ShowRestartHint();
    }
}
