namespace Content.Server._Polonium.Power.Generation.FissionGenerator;

/// <summary>
/// Raised on a nuclear reactor above its meltdown temperature, right before it would melt down.
/// </summary>
[ByRefEvent]
public record struct ReactorMeltdownAttemptEvent(bool Cancelled = false);
