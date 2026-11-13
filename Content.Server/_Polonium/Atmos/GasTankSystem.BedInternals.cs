using Content.Shared.Atmos.Components;
using Content.Shared.Buckle.Components;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class GasTankSystem
{
    /// <summary>
    /// Whether the tank sits inside something its user is buckled to, like a bed that supplies gas to a patient.
    /// Such a tank is in use even though the user does not hold it.
    /// </summary>
    private bool IsInBedOfUser(Entity<GasTankComponent> tank)
    {
        return tank.Comp.User is { } user
            && TryComp<BuckleComponent>(user, out var buckle)
            && buckle.BuckledTo is { } strap
            && strap == Transform(tank).ParentUid;
    }
}
