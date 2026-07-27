using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Throwing;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Charge;

public sealed partial class XenoChargeSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoChargeComponent, XenoChargeActionEvent>(OnCharge);
        SubscribeLocalEvent<XenoChargingComponent, StartCollideEvent>(OnCollide);
        SubscribeLocalEvent<XenoChargingComponent, LandEvent>(OnLand);
        SubscribeLocalEvent<XenoChargingComponent, StopThrowEvent>(OnStopThrow);
    }

    private void OnCharge(Entity<XenoChargeComponent> xeno, ref XenoChargeActionEvent args)
    {
        if (args.Handled || _timing.ApplyingState)
            return;

        var origin = _transform.GetMapCoordinates(xeno);
        var target = _transform.ToMapCoordinates(args.Target);
        if (origin.MapId != target.MapId)
            return;

        if ((target.Position - origin.Position).Length() > xeno.Comp.Range)
            return;

        args.Handled = true;

        if (_net.IsClient)
            return;

        var charging = EnsureComp<XenoChargingComponent>(xeno);
        charging.Damage = new(xeno.Comp.Damage);
        charging.HitEntities.Clear();
        Dirty(xeno, charging);

        _throwing.TryThrow(xeno, args.Target, xeno.Comp.ThrowSpeed, xeno, doSpin: false);
    }

    private void OnCollide(Entity<XenoChargingComponent> xeno, ref StartCollideEvent args)
    {
        if (_net.IsClient)
            return;

        var other = args.OtherEntity;
        if (other == xeno.Owner || xeno.Comp.HitEntities.Contains(other))
            return;

        if (!HasComp<MobStateComponent>(other) || HasComp<XenoComponent>(other))
            return;

        xeno.Comp.HitEntities.Add(other);
        _damageable.TryChangeDamage(other, xeno.Comp.Damage, origin: xeno);
    }

    private void OnLand(Entity<XenoChargingComponent> xeno, ref LandEvent args)
    {
        RemCompDeferred<XenoChargingComponent>(xeno);
    }

    private void OnStopThrow(Entity<XenoChargingComponent> xeno, ref StopThrowEvent args)
    {
        RemCompDeferred<XenoChargingComponent>(xeno);
    }
}
