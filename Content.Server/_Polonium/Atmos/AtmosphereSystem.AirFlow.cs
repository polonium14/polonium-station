using Content.Server._Polonium.Atmos.AirFlow;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    [Dependency] private AirFlowVisualsSystem _airFlowVisuals = default!;
}
