using Robust.Shared.Map;

namespace Content.Server._Polonium.Abductors;

/// <summary>
/// Someone taken aboard an abductor ship, with the place they were taken from.
/// </summary>
[RegisterComponent, Access(typeof(AbductorShipSystem))]
public sealed partial class AbductorSpecimenComponent : Component
{
    [ViewVariables]
    public EntityCoordinates Origin;
}
