namespace Content.Server._Polonium.Power.Generation.FissionGenerator;

/// <summary>
/// Raised on a nuclear reactor right after it melts down.
/// </summary>
[ByRefEvent]
public readonly record struct ReactorMeltdownEvent;
