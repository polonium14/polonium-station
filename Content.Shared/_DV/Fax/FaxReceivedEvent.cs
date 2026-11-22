namespace Content.Shared._DV.Fax;

/// <summary>
/// Raised on a fax machine when a document is queued for printing from the network or by an admin.
/// </summary>
[ByRefEvent]
public readonly record struct FaxReceivedEvent(string SenderName);
