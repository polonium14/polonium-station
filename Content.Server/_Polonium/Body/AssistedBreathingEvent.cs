namespace Content.Server._Polonium.Body;

/// <summary>
/// Raised on a body in critical condition, which cannot breathe on its own, to ask whether someone breathes for it.
/// </summary>
[ByRefEvent]
public record struct AssistedBreathingEvent(bool Assisted = false);
