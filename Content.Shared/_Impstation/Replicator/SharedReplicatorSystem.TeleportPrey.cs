using Content.Shared._Polonium.Replicator;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Teleportation.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Impstation.Replicator;

public abstract partial class SharedReplicatorSystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private SharedRandomGridTeleportSystem _randomGridTeleport = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private ReplicatorRecallImmunitySystem _recallImmunity = default!;

    private readonly HashSet<Entity<ReplicatorNestComponent>> _nearbyNests = new();

    private void InitializeTeleportPrey()
    {
        SubscribeLocalEvent<ReplicatorComponent, ReplicatorTeleportPreyActionEvent>(OnTeleportPrey);
        SubscribeLocalEvent<ReplicatorComponent, ReplicatorTeleportPreyDoAfterEvent>(OnTeleportPreyDoAfter);
    }

    private void OnTeleportPrey(Entity<ReplicatorComponent> ent, ref ReplicatorTeleportPreyActionEvent args)
    {
        if (args.Handled)
            return;

        var target = args.Target;

        if (HasComp<ReplicatorComponent>(target))
        {
            FailTeleportPrey(ent, "replicator-teleport-prey-fail-replicator");
            return;
        }

        if (HasComp<ReplicatorNestComponent>(target))
        {
            FailTeleportPrey(ent, "replicator-teleport-prey-fail-nest");
            return;
        }

        if (!HasComp<StunnedComponent>(target) && !HasComp<KnockedDownComponent>(target))
        {
            FailTeleportPrey(ent, "replicator-teleport-prey-fail-not-incapacitated");
            return;
        }

        if (_recallImmunity.IsImmune(target))
        {
            FailTeleportPrey(ent, "replicator-teleport-prey-fail-immune");
            return;
        }

        if (!TryComp<MobStateComponent>(target, out var mobState) || !_mobState.IsAlive(target, mobState))
        {
            FailTeleportPrey(ent, "replicator-teleport-prey-fail-dead");
            return;
        }

        var performerXform = Transform(ent);
        var targetXform = Transform(target);

        if (performerXform.GridUid == null || performerXform.GridUid != targetXform.GridUid)
        {
            FailTeleportPrey(ent, "replicator-teleport-prey-fail-grid");
            return;
        }

        if (!_transform.InRange((ent, performerXform), (target, targetXform), ent.Comp.TeleportPreyTargetRange))
        {
            FailTeleportPrey(ent, "replicator-teleport-prey-fail-range");
            return;
        }

        if (!_doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, ent, ent.Comp.TeleportPreyDelay, new ReplicatorTeleportPreyDoAfterEvent(), ent, target: target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            RequireCanInteract = false,
        }))
        {
            FailTeleportPrey(ent, "replicator-teleport-prey-fail-busy");
            return;
        }

        args.Handled = true;
    }

    private void OnTeleportPreyDoAfter(Entity<ReplicatorComponent> ent, ref ReplicatorTeleportPreyDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;

        if (args.Args.Target is not { } target || Deleted(target))
        {
            FailTeleportPrey(ent, "replicator-teleport-prey-fail-target-gone");
            return;
        }

        if (!HasComp<StunnedComponent>(target) && !HasComp<KnockedDownComponent>(target))
        {
            FailTeleportPrey(ent, "replicator-teleport-prey-fail-not-incapacitated");
            return;
        }

        if (_recallImmunity.IsImmune(target))
        {
            FailTeleportPrey(ent, "replicator-teleport-prey-fail-immune");
            return;
        }

        if (TryComp<PullableComponent>(target, out var pullable) && pullable.BeingPulled)
            _pulling.TryStopPull(target, pullable);

        if (!TryTeleportAway(ent, target))
        {
            FailTeleportPrey(ent, "replicator-teleport-prey-fail-no-space");
            return;
        }

        _stun.TryAddParalyzeDuration(target, ent.Comp.TeleportPreyParalyzeDuration, visualized: true);
        _recallImmunity.Apply(target, ent.Comp.TeleportPreyImmunityDuration);

        _audio.PlayPvs(ent.Comp.TeleportPreySound, target);

        _popup.PopupClient(Loc.GetString("replicator-teleport-prey-success"), ent, ent);
        _popup.PopupEntity(Loc.GetString("replicator-teleport-prey-success-target"), target, target, PopupType.Medium);
    }

    private void FailTeleportPrey(Entity<ReplicatorComponent> ent, string message)
    {
        _popup.PopupClient(Loc.GetString(message), ent, ent);
    }

    private bool TryTeleportAway(Entity<ReplicatorComponent> ent, EntityUid target)
    {
        var origin = Transform(ent).Coordinates;
        var min = ent.Comp.TeleportPreyRadiusMin;
        var max = ent.Comp.TeleportPreyRadiusMax;

        while (true)
        {
            if (_randomGridTeleport.TryTeleportEntity(target, origin, min, max, filter: coords => FarFromNests(ent, coords)))
                return true;

            if (max >= ent.Comp.TeleportPreyMaxRange || ent.Comp.TeleportPreyRangeStep <= 0)
                return false;

            min = max;
            max = MathF.Min(max + ent.Comp.TeleportPreyRangeStep, ent.Comp.TeleportPreyMaxRange);
        }
    }

    private bool FarFromNests(Entity<ReplicatorComponent> ent, EntityCoordinates coords)
    {
        _nearbyNests.Clear();
        _lookup.GetEntitiesInRange(_transform.ToMapCoordinates(coords), ent.Comp.TeleportPreyNestClearance, _nearbyNests);
        return _nearbyNests.Count == 0;
    }
}

public sealed partial class ReplicatorTeleportPreyActionEvent : EntityTargetActionEvent;

[Serializable, NetSerializable]
public sealed partial class ReplicatorTeleportPreyDoAfterEvent : SimpleDoAfterEvent;
