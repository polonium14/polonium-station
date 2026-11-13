namespace Content.Server._Polonium.StationEvents;

/// <summary>
/// Raised broadcast by the ion storm rule after it picks the station to affect.
/// </summary>
[ByRefEvent]
public readonly record struct IonStormStationEvent(EntityUid Station);
