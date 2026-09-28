namespace Content.Server._Polonium.EvacScenarios;

/// <summary>
/// Raised once, right before the emergency shuttles leave the station for CentComm.
/// Evac flight scenarios are picked here, and a scenario may change how long the flight lasts.
/// </summary>
/// <param name="TransitTime">Seconds from the jump to hyperspace until the round ends.</param>
[ByRefEvent]
public record struct EvacShuttlesLaunchingEvent(float TransitTime);

/// <summary>
/// Raised for every station's emergency shuttle right before it jumps towards CentComm.
/// A handler that sends the shuttle somewhere else sets <see cref="Handled"/>.
/// </summary>
/// <param name="StartupTime">Seconds until the shuttle jumps to hyperspace.</param>
/// <param name="TransitTime">Seconds from the jump to hyperspace until the round ends.</param>
[ByRefEvent]
public record struct EvacShuttleCourseEvent(EntityUid Station, EntityUid Shuttle, float StartupTime, float TransitTime)
{
    public bool Handled;
}
