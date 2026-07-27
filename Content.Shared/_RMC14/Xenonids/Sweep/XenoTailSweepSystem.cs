using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Sweep;

public sealed partial class XenoTailSweepSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _entityLookup = default!;
    [Dependency] private SharedInteractionSystem _interact = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private XenoPlasmaSystem _plasma = default!;

    private readonly HashSet<EntityUid> _hit = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoTailSweepComponent, XenoTailSweepActionEvent>(OnXenoTailSweepAction);
    }

    private void OnXenoTailSweepAction(Entity<XenoTailSweepComponent> xeno, ref XenoTailSweepActionEvent args)
    {
        if (args.Handled || _timing.ApplyingState)
            return;

        if (!TryComp(xeno, out TransformComponent? transform))
            return;

        var ev = new XenoTailSweepAttemptEvent();
        RaiseLocalEvent(xeno, ref ev);
        if (ev.Cancelled)
            return;

        if (!_plasma.TryRemovePlasmaPopup(xeno.Owner, xeno.Comp.PlasmaCost))
            return;

        args.Handled = true;
        _audio.PlayPredicted(xeno.Comp.Sound, xeno, xeno);

        if (_net.IsClient)
            return;

        _hit.Clear();
        _entityLookup.GetEntitiesInRange(transform.Coordinates, xeno.Comp.Range, _hit);

        var origin = _transform.GetWorldPosition(xeno);
        foreach (var mob in _hit)
        {
            if (mob == xeno.Owner)
                continue;

            if (!HasComp<MobStateComponent>(mob))
                continue;

            if (HasComp<XenoComponent>(mob) || HasComp<XenoFriendlyComponent>(mob))
                continue;

            if (!_interact.InRangeUnobstructed(xeno.Owner, mob, xeno.Comp.Range))
                continue;

            _damageable.TryChangeDamage(mob, xeno.Comp.Damage, origin: xeno);
            _stun.TryUpdateParalyzeDuration(mob, xeno.Comp.ParalyzeTime);

            var targetPos = _transform.GetWorldPosition(mob);
            var direction = targetPos - origin;
            if (direction != default)
                _throwing.TryThrow(mob, direction.Normalized() * xeno.Comp.KnockBackDistance, 8f, xeno);

            _audio.PlayPvs(xeno.Comp.HitSound, mob);
        }
    }
}
