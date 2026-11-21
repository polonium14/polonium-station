namespace Content.Server._Polonium.Lathe;

/// <summary>
/// Raised on a lathe when it stops: the queue ran out, the print was aborted or the power went.
/// </summary>
[ByRefEvent]
public readonly record struct LatheRunEndedEvent;
