using Content.Shared.ActionBlocker;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Polonium.Mobs;

/// <summary>
/// Follows when a critical mob crosses its hard crit threshold and refreshes what depends on it:
/// movement, speed, standing and the crit actions. What soft crit allows is decided in
/// <see cref="MobStateSystem.IsSoftCritical"/> and the checks that call it.
/// </summary>
public sealed partial class SoftCritSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private MobStateActionsSystem _mobStateActions = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SoftCritComponent, DamageChangedEvent>(OnDamageChanged, after: [typeof(MobThresholdSystem)]);
        SubscribeLocalEvent<SoftCritComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<SoftCritComponent, AfterAutoHandleStateEvent>(OnAfterHandleState);
        SubscribeLocalEvent<SoftCritComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<SoftCritComponent, PullStartedMessage>(OnPullChanged);
        SubscribeLocalEvent<SoftCritComponent, PullStoppedMessage>(OnPullChanged);
        SubscribeLocalEvent<SoftCritStandStatusEffectComponent, StatusEffectAppliedEvent>(OnStandApplied);
        SubscribeLocalEvent<SoftCritStandStatusEffectComponent, StatusEffectRemovedEvent>(OnStandRemoved);
    }

    private void OnDamageChanged(Entity<SoftCritComponent> ent, ref DamageChangedEvent args)
    {
        UpdateActive(ent);
    }

    private void OnMobStateChanged(Entity<SoftCritComponent> ent, ref MobStateChangedEvent args)
    {
        UpdateActive(ent);
    }

    private void OnAfterHandleState(Entity<SoftCritComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        _blocker.UpdateCanMove(ent);
        _movement.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void OnRefreshSpeed(Entity<SoftCritComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.Active)
            args.ModifySpeed(ent.Comp.SpeedModifier);
    }

    private void OnPullChanged(EntityUid uid, SoftCritComponent component, PullMessage args)
    {
        if (args.PulledUid == uid && component.Active)
            _blocker.UpdateCanMove(uid);
    }

    private void OnStandApplied(Entity<SoftCritStandStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        // Standing is networked: a client applying a server state must not stand or fall on its own.
        if (!_timing.ApplyingState && _mobState.IsSoftCritical(args.Target))
            _standing.Stand(args.Target);
    }

    private void OnStandRemoved(Entity<SoftCritStandStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (!_timing.ApplyingState && _mobState.IsSoftCritical(args.Target))
            _standing.Down(args.Target);
    }

    private void UpdateActive(Entity<SoftCritComponent> ent)
    {
        // The server state brings Active along, and OnAfterHandleState refreshes what depends on it.
        if (_timing.ApplyingState)
            return;

        var active = _mobState.IsSoftCritical(ent);
        var hardCrit = !active && _mobState.IsCritical(ent);
        if (active == ent.Comp.Active && hardCrit == ent.Comp.HardCrit)
            return;

        var changed = active != ent.Comp.Active;
        ent.Comp.Active = active;
        ent.Comp.HardCrit = hardCrit;
        Dirty(ent);

        if (!changed)
            return;

        _blocker.UpdateCanMove(ent);
        _movement.RefreshMovementSpeedModifiers(ent.Owner);

        if (active && _statusEffects.HasEffectComp<SoftCritStandStatusEffectComponent>(ent))
            _standing.Stand(ent);
        else if (!active && _mobState.IsCritical(ent))
            _standing.Down(ent); // Into hard crit: whoever stood on epinephrine falls.

        // The crit actions are granted per mob state, and soft and hard crit share one.
        if (_net.IsServer
            && TryComp<MobStateActionsComponent>(ent, out var actions)
            && TryComp<MobStateComponent>(ent, out var mobState))
            _mobStateActions.ComposeActions(ent, actions, mobState.CurrentState);
    }
}
