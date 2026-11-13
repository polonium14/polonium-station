using Content.Shared._Polonium.DeathBlackout;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.CCVar;
using Content.Shared.Ghost.Systems;
using Content.Shared.Gibbing;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.DeathBlackout;

/// <summary>
/// Starts the death blackout when a mob with a mind dies or is gibbed while in critical state, and blocks ghosting until it ends.
/// An instant death, without going through critical state first, gets no blackout.
/// </summary>
public sealed partial class DeathBlackoutSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;

    [SubscribeLocalEvent]
    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        var ent = args.Target;

        if (args.NewMobState != MobState.Dead)
        {
            // revived, so the blackout must not linger
            if (args.OldMobState == MobState.Dead)
                RemComp<DeathBlackoutComponent>(ent);

            return;
        }

        if (args.OldMobState != MobState.Critical)
            return;

        if (!TryComp<MindContainerComponent>(ent, out var mindContainer) || !mindContainer.HasMind)
            return;

        ApplyDeathBlackout(ent);
    }

    // gibbing skips the dead state and moves the mind into the brain, so the brain carries the blackout
    [SubscribeLocalEvent]
    private void OnBeingGibbed(Entity<BrainComponent> ent, ref BodyRelayedEvent<BeingGibbedEvent> args)
    {
        if (!TryComp<MindContainerComponent>(args.Body, out var mindContainer) || !mindContainer.HasMind)
            return;

        // a dead body already got the blackout when it died, and a living one dies instantly
        if (!TryComp<MobStateComponent>(args.Body, out var mobState) || mobState.CurrentState != MobState.Critical)
            return;

        ApplyDeathBlackout(ent);
    }

    private void ApplyDeathBlackout(EntityUid ent)
    {
        if (!_cfg.GetCVar(CCVars.DeathBlackoutEnabled))
            return;

        var blackout = EnsureComp<DeathBlackoutComponent>(ent);
        blackout.EndTime = _timing.CurTime + TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.DeathBlackoutDuration));
        Dirty(ent, blackout);
    }

    // ghosting out would skip the blackout entirely
    [SubscribeLocalEvent]
    private void OnGhostAttempt(Entity<DeathBlackoutComponent> ent, ref GhostAttemptEvent args)
    {
        if (!_cfg.GetCVar(CCVars.DeathBlackoutEnabled))
            return;

        if (_timing.CurTime < ent.Comp.EndTime)
            args.Cancelled = true;
    }
}
