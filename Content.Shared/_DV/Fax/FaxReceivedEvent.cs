namespace Content.Shared._DV.Fax;

/// <summary>
/// Raised on a fax machine when a document arrives from the network or from an admin, before it is queued.
/// </summary>
[ByRefEvent]
public record struct FaxReceivedEvent(string SenderName)
{
    /// <summary>
    /// Drops the document instead of queueing it.
    /// </summary>
    public bool Cancelled;

    /// <summary>
    /// Skips the popup above the machine.
    /// </summary>
    public bool Silent;
}
