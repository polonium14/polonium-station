using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Content.Shared.Vehicles;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;

namespace Content.Server.Vehicles;

public sealed partial class TankPhysicsSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TankTurretComponent, PreventCollideEvent>(OnTankPreventCollide);
    }

    private void OnTankPreventCollide(EntityUid uid, TankTurretComponent component, ref PreventCollideEvent args)
    {
        var other = args.OtherEntity;

        if (TryComp(other, out ProjectileComponent? proj))
        {
            if (proj.Shooter == uid || proj.Weapon == uid)
                args.Cancelled = true;
            return;
        }

        if (HasComp<MobStateComponent>(other) || HasComp<TankTurretComponent>(other))
            return;

        if (HasComp<ItemComponent>(other) || HasComp<ThrownItemComponent>(other))
        {
            args.Cancelled = true;
            return;
        }

        if (TryComp(other, out TransformComponent? ox) && ox.Anchored)
            return;

        if (TryComp(other, out PhysicsComponent? phys) && phys.BodyType == BodyType.Static)
            return;

        args.Cancelled = true;
    }
}