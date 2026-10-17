namespace Content.Shared._Polonium.Tutorial.Lobby;
public sealed class TutorialLobbyProgress
{
    public string CurrentStepId { get; set; } = string.Empty;
    public bool IsCompleted { get; set; } = false;
    public bool IsPaused { get; set; } = false;

    /// <summary>Player said no or hit skip. Stops the lobby from offering it again unprompted.</summary>
    public bool HasDeclined { get; set; } = false;
}
