using Content.Shared.Gravity;

namespace Content.Shared.Vehicles;

public sealed partial class TankMagSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TankTurretComponent, IsWeightlessEvent>(OnIsWeightless);
    }

    private void OnIsWeightless(EntityUid uid, TankTurretComponent component, ref IsWeightlessEvent args)
    {
        if (args.Handled)
            return;

        var xform = Transform(uid);
        if (xform.GridUid != null)
        {
            args.IsWeightless = false;
            args.Handled = true;
        }
    }
}