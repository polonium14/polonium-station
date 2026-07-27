using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Fling;

public sealed class XenoFlingSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private MovementModStatusSystem _movementMod = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoFlingComponent, XenoFlingActionEvent>(OnXenoFlingAction);
    }

    private void OnXenoFlingAction(Entity<XenoFlingComponent> xeno, ref XenoFlingActionEvent args)
    {
        if (args.Handled || _timing.ApplyingState)
            return;

        if (!_interaction.InRangeUnobstructed(args.Performer, args.Target, xeno.Comp.Range))
            return;

        if (!HasComp<MobStateComponent>(args.Target))
            return;

        if (HasComp<XenoComponent>(args.Target) || HasComp<XenoFriendlyComponent>(args.Target))
            return;

        var attempt = new XenoFlingAttemptEvent();
        RaiseLocalEvent(xeno, ref attempt);
        if (attempt.Cancelled)
            return;

        args.Handled = true;

        if (_net.IsClient)
            return;

        _audio.PlayPvs(xeno.Comp.Sound, xeno);

        var damage = _damageable.TryChangeDamage(args.Target, xeno.Comp.Damage, origin: xeno);
        _ = damage;

        var origin = _transform.GetWorldPosition(xeno);
        var target = _transform.GetWorldPosition(args.Target);
        var direction = target - origin;
        if (direction != default)
            _throwing.TryThrow(args.Target, direction.Normalized() * xeno.Comp.ThrowRange, xeno.Comp.ThrowSpeed, xeno);

        _stun.TryUpdateParalyzeDuration(args.Target, xeno.Comp.ParalyzeTime);
        _movementMod.TryUpdateMovementSpeedModDuration(
            args.Target,
            MovementModStatusSystem.TaserSlowdown,
            xeno.Comp.SlowTime,
            xeno.Comp.SlowMultiplier);
    }
}

[ByRefEvent]
public record struct XenoFlingAttemptEvent
{
    public bool Cancelled;
}
