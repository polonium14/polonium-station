using System.Numerics;
using Content.Shared._RMC14.Xenonids.Rest;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Lunge;

public sealed partial class XenoLungeSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ThrownItemSystem _thrownItem = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<ThrownItemComponent> _thrownItemQuery;

    public override void Initialize()
    {
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _thrownItemQuery = GetEntityQuery<ThrownItemComponent>();

        SubscribeLocalEvent<XenoLungeComponent, XenoLungeActionEvent>(OnXenoLungeAction);
        SubscribeLocalEvent<XenoActiveLungeComponent, ThrowDoHitEvent>(OnXenoLungeHit);
        SubscribeLocalEvent<XenoActiveLungeComponent, LandEvent>(OnXenoLungeLand);
        SubscribeLocalEvent<XenoActiveLungeComponent, StopThrowEvent>(OnXenoLungeStopThrow);
    }

    private void OnXenoLungeAction(Entity<XenoLungeComponent> xeno, ref XenoLungeActionEvent args)
    {
        if (args.Handled || _timing.ApplyingState)
            return;

        var target = args.Target;
        if (!_mobState.IsAlive(target) || HasComp<XenoComponent>(target) || HasComp<XenoFriendlyComponent>(target))
            return;

        var attempt = new XenoLungeAttemptEvent();
        RaiseLocalEvent(xeno, ref attempt);
        if (attempt.Cancelled)
            return;

        args.Handled = true;

        var origin = _transform.GetMapCoordinates(xeno);
        var targetCoords = _transform.GetMapCoordinates(target);
        if (origin.MapId != targetCoords.MapId)
            return;

        var diff = targetCoords.Position - origin.Position;
        if (diff == Vector2.Zero)
            return;

        diff = diff.Normalized() * xeno.Comp.Range;

        var active = EnsureComp<XenoActiveLungeComponent>(xeno);
        active.Origin = origin;
        active.Charge = diff;
        active.Target = target;
        active.StunTime = xeno.Comp.StunTime;
        Dirty(xeno, active);

        if (_physicsQuery.TryComp(xeno, out var physics))
            _physics.SetLinearVelocity(xeno, Vector2.Zero, body: physics);

        _throwing.TryThrow(xeno, diff, xeno.Comp.ThrowSpeed, xeno, doSpin: false, recoil: false, pushbackRatio: 0);

        if (_physicsQuery.TryComp(xeno, out physics))
        {
            foreach (var ent in _physics.GetContactingEntities(xeno.Owner, physics))
            {
                if (ent != target)
                    continue;

                if (ApplyLungeHit((xeno, active), ent))
                    return;
            }
        }
    }

    private void OnXenoLungeHit(Entity<XenoActiveLungeComponent> xeno, ref ThrowDoHitEvent args)
    {
        if (!_mobState.IsAlive(xeno) || HasComp<XenoRestingComponent>(xeno))
        {
            RemCompDeferred<XenoActiveLungeComponent>(xeno);
            return;
        }

        ApplyLungeHit(xeno, args.Target);
    }

    private void OnXenoLungeLand(Entity<XenoActiveLungeComponent> ent, ref LandEvent args)
    {
        RemCompDeferred<XenoActiveLungeComponent>(ent);
    }

    private void OnXenoLungeStopThrow(Entity<XenoActiveLungeComponent> ent, ref StopThrowEvent args)
    {
        RemCompDeferred<XenoActiveLungeComponent>(ent);
    }

    private bool ApplyLungeHit(Entity<XenoActiveLungeComponent> xeno, EntityUid targetId)
    {
        if (_mobState.IsDead(targetId))
            return false;

        if (HasComp<XenoComponent>(targetId) || HasComp<XenoFriendlyComponent>(targetId))
            return true;

        if (_physicsQuery.TryGetComponent(xeno, out var physics) &&
            _thrownItemQuery.TryGetComponent(xeno, out var thrown))
        {
            _thrownItem.LandComponent(xeno, thrown, physics, true);
            _thrownItem.StopThrow(xeno, thrown);
        }

        if (_net.IsServer)
        {
            _stun.TryUpdateParalyzeDuration(targetId, xeno.Comp.StunTime);
            _pulling.TryStartPull(xeno, targetId);
        }

        StopLunge(xeno);
        RemCompDeferred<XenoActiveLungeComponent>(xeno);
        return true;
    }

    private void StopLunge(EntityUid lunging)
    {
        if (!_physicsQuery.TryGetComponent(lunging, out var physics))
            return;

        _physics.SetLinearVelocity(lunging, Vector2.Zero, body: physics);
        _physics.SetBodyStatus(lunging, physics, BodyStatus.OnGround);
    }
}

[ByRefEvent]
public record struct XenoLungeAttemptEvent
{
    public bool Cancelled;
}
