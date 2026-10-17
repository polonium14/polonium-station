using Content.Shared.Item;
using Content.Shared.Vehicle.Components;
using Content.Shared.Vehicles;

namespace Content.Server.Vehicles;

public sealed partial class TankHandsSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VehicleOperatorComponent, PickupAttemptEvent>(OnPickupAttempt);
    }

    private void OnPickupAttempt(EntityUid uid, VehicleOperatorComponent component, PickupAttemptEvent args)
    {
        if (component.Vehicle is not { } tank)
            return;

        if (!HasComp<TankTurretComponent>(tank))
            return;

        args.Cancel();
    }
}