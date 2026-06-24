namespace Content.Shared.GameTicking;

/// <summary>
/// Raised after a round has fully started (players spawned, round in progress).
/// </summary>
public sealed class RoundStartedEvent : EntityEventArgs
{
    public int RoundId { get; }

    public RoundStartedEvent(int roundId)
    {
        RoundId = roundId;
    }
}
