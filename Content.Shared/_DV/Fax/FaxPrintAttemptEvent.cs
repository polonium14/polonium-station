namespace Content.Shared._DV.Fax;

/// <summary>
/// Raised on a fax machine before it starts printing the next queued document.
/// Cancelling keeps the document in the queue, and the machine tries again on the next tick.
/// </summary>
[ByRefEvent]
public record struct FaxPrintAttemptEvent
{
    public bool Cancelled;
}
