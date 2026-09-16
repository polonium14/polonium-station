namespace Content.Shared._Polonium.Tutorial.Lobby;

public abstract class SharedTutorialLobbyManager
{
}

public interface IClientsideNavTutorialStep
{
    string StepId { get; }

    bool Execute();

    void Cleanup();

    void OnReenter();

    bool CanExecute();
}
