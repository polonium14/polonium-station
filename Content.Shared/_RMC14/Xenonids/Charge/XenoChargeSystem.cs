using System.Numerics;
using Content.Shared._RMC14.Xenonids.Animation;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Charge;

public sealed partial class XenoChargeSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedColorFlashEffectSystem _colorFlash = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MovementModStatusSystem _movementMod = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private ThrownItemSystem _thrownItem = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private XenoAnimationsSystem _xenoAnimations = default!;
    [Dependency] private XenoPlasmaSystem _plasma = default!;

    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<ThrownItemComponent> _thrownItemQuery;

    public override void Initialize()
    {
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _thrownItemQuery = GetEntityQuery<ThrownItemComponent>();

        SubscribeLocalEvent<XenoChargeComponent, XenoChargeActionEvent>(OnXenoChargeAction);
        SubscribeLocalEvent<XenoChargeComponent, XenoChargeDoAfterEvent>(OnXenoChargeDoAfter);
        SubscribeLocalEvent<XenoChargeComponent, ThrowDoHitEvent>(OnXenoChargeHit);
        SubscribeLocalEvent<XenoChargeComponent, StopThrowEvent>(OnXenoChargeStop);
    }

    private void OnXenoChargeAction(Entity<XenoChargeComponent> xeno, ref XenoChargeActionEvent args)
    {
        if (args.Handled || _timing.ApplyingState)
            return;

        var origin = _transform.GetMapCoordinates(xeno);
        var target = _transform.ToMapCoordinates(args.Target);
        if (origin.MapId != target.MapId)
            return;

        if ((target.Position - origin.Position).Length() > xeno.Comp.Range)
            return;

        if (!_plasma.TryRemovePlasmaPopup(xeno.Owner, xeno.Comp.PlasmaCost))
            return;

        args.Handled = true;

        _movementMod.TryUpdateMovementSpeedModDuration(
            xeno.Owner,
            MovementModStatusSystem.TaserSlowdown,
            xeno.Comp.ChargeDelay + TimeSpan.FromSeconds(0.55),
            0f);

        var ev = new XenoChargeDoAfterEvent(GetNetCoordinates(args.Target));
        var doAfter = new DoAfterArgs(EntityManager, xeno, xeno.Comp.ChargeDelay, ev, xeno)
        {
            BreakOnMove = true,
            Hidden = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnXenoChargeDoAfter(Entity<XenoChargeComponent> xeno, ref XenoChargeDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        var coordinates = GetCoordinates(args.Coordinates);
        var origin = _transform.GetMapCoordinates(xeno);
        var target = _transform.ToMapCoordinates(coordinates);
        if (origin.MapId != target.MapId)
            return;

        var diff = target.Position - origin.Position;
        if (diff == Vector2.Zero)
            return;

        diff = diff.Normalized() * xeno.Comp.Range;

        xeno.Comp.Charge = diff;
        Dirty(xeno);

        if (_physicsQuery.TryComp(xeno, out var physics))
            _physics.SetLinearVelocity(xeno, Vector2.Zero, body: physics);

        _throwing.TryThrow(xeno, diff, xeno.Comp.Strength, xeno, doSpin: false, recoil: false, pushbackRatio: 0);
    }

    private void OnXenoChargeHit(Entity<XenoChargeComponent> xeno, ref ThrowDoHitEvent args)
    {
        var targetId = args.Target;
        if (targetId == xeno.Owner)
            return;

        if (!HasComp<MobStateComponent>(targetId))
            return;

        if (HasComp<XenoComponent>(targetId) || HasComp<XenoFriendlyComponent>(targetId))
            return;

        StopCharge(xeno);

        if (_net.IsClient)
            return;

        _audio.PlayPvs(xeno.Comp.Sound, xeno);

        if (xeno.Comp.Damage.GetTotal() > FixedPoint2.Zero)
        {
            _damageable.TryChangeDamage(targetId, xeno.Comp.Damage, origin: xeno);
            var filter = Filter.Pvs(targetId, entityManager: EntityManager).RemoveWhereAttachedEntity(o => o == xeno.Owner);
            _colorFlash.RaiseEffect(Color.Red, new List<EntityUid> { targetId }, filter);
        }

        _stun.TryUpdateParalyzeDuration(targetId, xeno.Comp.StunTime);

        var origin = _transform.GetWorldPosition(xeno);
        var targetPos = _transform.GetWorldPosition(targetId);
        var direction = targetPos - origin;
        if (direction != default)
            _throwing.TryThrow(targetId, direction.Normalized() * xeno.Comp.Knockback, 10f, xeno);
    }

    private void OnXenoChargeStop(Entity<XenoChargeComponent> xeno, ref StopThrowEvent args)
    {
        if (xeno.Comp.Charge == null)
            return;

        xeno.Comp.Charge = null;
        Dirty(xeno);

        if (_net.IsClient)
            return;

        foreach (var slower in _lookup.GetEntitiesInRange<MobStateComponent>(_transform.GetMapCoordinates(xeno), xeno.Comp.SlowRange))
        {
            if (slower.Owner == xeno.Owner)
                continue;

            if (HasComp<XenoComponent>(slower) || HasComp<XenoFriendlyComponent>(slower))
                continue;

            _movementMod.TryUpdateMovementSpeedModDuration(
                slower.Owner,
                MovementModStatusSystem.TaserSlowdown,
                xeno.Comp.SlowTime,
                xeno.Comp.SlowMultiplier);
        }
    }

    private void StopCharge(Entity<XenoChargeComponent> xeno)
    {
        if (_physicsQuery.TryGetComponent(xeno, out var physics) &&
            _thrownItemQuery.TryGetComponent(xeno, out var thrown))
        {
            _thrownItem.LandComponent(xeno, thrown, physics, true);
            _thrownItem.StopThrow(xeno, thrown);
        }

        if (_timing.IsFirstTimePredicted && xeno.Comp.Charge is { } charge)
        {
            xeno.Comp.Charge = null;
            _xenoAnimations.PlayLungeAnimationEvent(xeno, charge);
        }

        if (_physicsQuery.TryGetComponent(xeno, out physics))
        {
            _physics.SetLinearVelocity(xeno, Vector2.Zero, body: physics);
            _physics.SetBodyStatus(xeno, physics, BodyStatus.OnGround);
        }
    }
}
