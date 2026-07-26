// SPDX-FileCopyrightText: 2026 Maciej Walendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Shitmed.CCVar;
using Content.Shared._Shitmed.Medical.Surgery.Consciousness.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Shared.Body.Systems;

[UsedImplicitly]
public abstract partial class SharedBloodstreamSystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private WoundSystem _wound = default!;
    [Dependency] private INetManager _net = default!;

    private void InitializeWounds()
    {
        SubscribeLocalEvent<BleedInflicterComponent, WoundSeverityPointChangedEvent>(OnBleedInflicterSeverityUpdate);
        SubscribeLocalEvent<BleedRemoverComponent, WoundSeverityPointChangedEvent>(OnBleedRemoverSeverityUpdate);
        SubscribeLocalEvent<BleedInflicterComponent, WoundHealAttemptEvent>(OnWoundHealAttempt);
        SubscribeLocalEvent<BleedInflicterComponent, WoundAddedEvent>(OnWoundAdded);
    }

    private void UpdateWounds(float frameTime)
    {
        var bleedsQuery = EntityQueryEnumerator<BleedInflicterComponent>();
        while (bleedsQuery.MoveNext(out var ent, out var bleeds))
        {
            var canBleed = CanWoundBleed(ent, bleeds) && bleeds.BleedingAmount > 0;
            if (canBleed != bleeds.IsBleeding)
                Dirty(ent, bleeds);

            bleeds.IsBleeding = canBleed;

            if (!bleeds.IsBleeding)
                continue;

            var totalTime = bleeds.ScalingFinishesAt - bleeds.ScalingStartsAt;
            var currentTime = bleeds.ScalingFinishesAt - _timing.CurTime;

            if (totalTime <= currentTime || bleeds.Scaling >= bleeds.ScalingLimit)
                continue;

            var newBleeds = FixedPoint2.Clamp(
                (totalTime / currentTime) / (bleeds.ScalingLimit - bleeds.Scaling),
                0,
                bleeds.ScalingLimit);

            bleeds.Scaling = newBleeds;
            Dirty(ent, bleeds);
        }

        if (!_net.IsServer)
            return;

        var woundableQuery = EntityQueryEnumerator<WoundableComponent>();
        while (woundableQuery.MoveNext(out var woundableEnt, out var woundable))
        {
            _wound.RecomputeWoundableBleeds(woundableEnt, woundable);
        }

        var bloodstreamQuery = EntityQueryEnumerator<BloodstreamComponent, BodyComponent>();
        while (bloodstreamQuery.MoveNext(out var mob, out var bloodstream, out var mobBody))
        {
            if (mobBody.Organs is null)
                continue;

            var total = FixedPoint2.Zero;
            foreach (var organ in mobBody.Organs.ContainedEntities)
            {
                if (TryComp<WoundableComponent>(organ, out var organWoundable))
                    total += organWoundable.Bleeds;
            }

            var totalFloat = total.Float();
            if (bloodstream.BleedAmountFromWounds == totalFloat)
                continue;

            bloodstream.BleedAmountFromWounds = totalFloat;
            RecomputeBleedAmount(mob, bloodstream);
        }
    }

    /// <summary>
    /// Add a bleed-ability modifier on woundable
    /// </summary>
    public bool TryAddBleedModifier(
        EntityUid woundable,
        string identifier,
        int priority,
        bool canBleed,
        bool force = false,
        WoundableComponent? woundableComp = null)
    {
        if (!Resolve(woundable, ref woundableComp))
            return false;

        foreach (var woundEnt in _wound.GetWoundableWounds(woundable, woundableComp))
        {
            if (!TryComp<BleedInflicterComponent>(woundEnt, out var bleedsComp))
                continue;

            if (TryAddBleedModifier(woundEnt, identifier, priority, canBleed, bleedsComp))
                continue;

            if (!force)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Add a bleed-ability modifier
    /// </summary>
    public bool TryAddBleedModifier(
        EntityUid uid,
        string identifier,
        int priority,
        bool canBleed,
        BleedInflicterComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return false;

        if (!comp.BleedingModifiers.TryAdd(identifier, (priority, canBleed)))
            return false;

        Dirty(uid, comp);
        return true;
    }

    /// <summary>
    /// Remove a bleed-ability modifier from a woundable
    /// </summary>
    public bool TryRemoveBleedModifier(
        EntityUid uid,
        string identifier,
        bool force = false,
        WoundableComponent? woundable = null)
    {
        if (!Resolve(uid, ref woundable))
            return false;

        foreach (var woundEnt in _wound.GetWoundableWounds(uid, woundable))
        {
            if (!TryComp<BleedInflicterComponent>(woundEnt, out var bleedsComp))
                continue;

            if (TryRemoveBleedModifier(woundEnt, identifier, bleedsComp))
                continue;

            if (!force)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Remove a bleed-ability modifier
    /// </summary>
    public bool TryRemoveBleedModifier(
        EntityUid uid,
        string identifier,
        BleedInflicterComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return false;

        if (!comp.BleedingModifiers.Remove(identifier))
            return false;

        Dirty(uid, comp);
        return true;
    }

    /// <summary>
    /// Redact a modifiers meta data
    /// </summary>
    public bool ChangeBleedsModifierMetadata(
        EntityUid wound,
        string identifier,
        int priority,
        bool? canBleed,
        BleedInflicterComponent? bleeds = null)
    {
        if (!Resolve(wound, ref bleeds))
            return false;

        if (!bleeds.BleedingModifiers.TryGetValue(identifier, out var pair))
            return false;

        bleeds.BleedingModifiers[identifier] = (Priority: priority, CanBleed: canBleed ?? pair.CanBleed);
        return true;
    }

    /// <summary>
    /// Redact a modifiers meta data
    /// </summary>
    public bool ChangeBleedsModifierMetadata(
        EntityUid wound,
        string identifier,
        bool canBleed,
        int? priority,
        BleedInflicterComponent? bleeds = null)
    {
        if (!Resolve(wound, ref bleeds))
            return false;

        if (!bleeds.BleedingModifiers.TryGetValue(identifier, out var pair))
            return false;

        bleeds.BleedingModifiers[identifier] = (Priority: priority ?? pair.Priority, CanBleed: canBleed);
        return true;
    }

    /// <summary>
    /// Self-explanatory
    /// </summary>
    public bool CanWoundBleed(EntityUid uid, BleedInflicterComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return false;

        var nearestModifier = comp.BleedingModifiers.FirstOrNull();
        if (nearestModifier == null)
            return true; // No modifiers. return true

        var lastCanBleed = true;
        var lastPriority = 0;
        foreach (var (_, pair) in comp.BleedingModifiers)
        {
            if (pair.Priority <= lastPriority)
                continue;

            lastPriority = pair.Priority;
            lastCanBleed = pair.CanBleed;
        }

        return lastCanBleed;
    }

    private void OnWoundAdded(EntityUid uid, BleedInflicterComponent component, ref WoundAddedEvent args)
    {
        // WoundableComponent.CanBleed is a static property of the limb, so a woundable that can
        // never bleed never accrues. CanWoundBleed is deliberately NOT checked here: it's a
        // runtime suppression (tourniquets flip it via BleedingModifiers), and UpdateWounds
        // re-derives IsBleeding from it every tick while RecomputeWoundableBleeds only sums
        // wounds that are actually bleeding - so the effect is already held off. Skipping the
        // accrual too would permanently under-credit any wound inflicted under a tourniquet,
        // which then bleeds far less than its severity once the clamp comes off.
        if (args.Component.WoundSeverityPoint < component.SeverityThreshold
            || !args.Woundable.CanBleed)
            return;

        // wounds that BLEED will not HEAL.
        component.BleedingAmountRaw = args.Component.WoundSeverityPoint * _cfg.GetCVar(SurgeryCVars.BleedingSeverityTrade);

        var formula = (float) (args.Component.WoundSeverityPoint / _cfg.GetCVar(SurgeryCVars.BleedsScalingTime) * component.ScalingSpeed);
        component.ScalingFinishesAt = _timing.CurTime + TimeSpan.FromSeconds(formula);
        component.ScalingStartsAt = _timing.CurTime;
        component.IsBleeding = CanWoundBleed(uid, component);

        Dirty(uid, component);
    }

    private void OnWoundHealAttempt(EntityUid uid, BleedInflicterComponent component, ref WoundHealAttemptEvent args)
    {
        if (args.IgnoreBlockers)
            return;

        if (component.IsBleeding)
            args.Cancelled = true;
    }

    private void OnBleedInflicterSeverityUpdate(EntityUid uid,
        BleedInflicterComponent component,
        ref WoundSeverityPointChangedEvent args)
    {
        // Same reasoning as OnWoundAdded: accrual tracks severity regardless of a tourniquet,
        // and IsBleeding below (plus UpdateWounds every tick) is what actually holds the bleed
        // off while the limb is clamped.
        if (!TryComp<WoundableComponent>(args.Component.HoldingWoundable, out var woundable)
            || !woundable.CanBleed)
            return;

        if (args.NewSeverity < args.OldSeverity)
        {
            // Healed back under the gate that would have started the bleed in the first place -
            // OnWoundAdded and the growth path below both refuse to bleed there, so leaving a
            // proportional trickle here is the only way a sub-threshold wound bleeds at all.
            // It also traps the wound: OnWoundHealAttempt blocks passive healing while
            // IsBleeding, so the residual would keep the wound from ever closing on its own.
            if (args.NewSeverity < component.SeverityThreshold)
            {
                component.BleedingAmountRaw = FixedPoint2.Zero;
                component.IsBleeding = false;
                Dirty(uid, component);
                return;
            }

            var healedCap = args.NewSeverity * _cfg.GetCVar(SurgeryCVars.BleedingSeverityTrade);
            if (component.BleedingAmountRaw > healedCap)
            {
                component.BleedingAmountRaw = healedCap;
                Dirty(uid, component);
            }

            return;
        }

        if (args.NewSeverity < component.SeverityThreshold)
            return;

        // Growing past the threshold for the first time has to land on the same bleed a wound
        // born at that size gets from OnWoundAdded - the sub-threshold portion never accrued,
        // so seed from the whole severity rather than just this hit's delta. Growth on a wound
        // that was already past the threshold keeps accruing by delta, so a partial cautery
        // (which lowers BleedingAmountRaw without touching severity) isn't handed back.
        var crossedFromBelow = args.OldSeverity < component.SeverityThreshold;

        if (crossedFromBelow)
            component.BleedingAmountRaw = args.NewSeverity * _cfg.GetCVar(SurgeryCVars.BleedingSeverityTrade);
        else
            component.BleedingAmountRaw += (args.NewSeverity - args.OldSeverity) * _cfg.GetCVar(SurgeryCVars.BleedingSeverityTrade);

        var formula = (float) (args.NewSeverity / _cfg.GetCVar(SurgeryCVars.BleedsScalingTime) * component.ScalingSpeed);
        component.ScalingFinishesAt = _timing.CurTime + TimeSpan.FromSeconds(formula);
        component.ScalingStartsAt = _timing.CurTime;

        // The CanWoundBleed test matters here: under a tourniquet UpdateWounds keeps IsBleeding
        // false every tick, so without it each further hit would read as a fresh reopening and
        // stack another +0.6 onto ScalingLimit without bound.
        if (CanWoundBleed(uid, component) && !component.IsBleeding)
        {
            // Only a genuine reopening earns the scaling bump - a wound whose bleed was stopped
            // and then torn open again. A wound merely growing past the threshold for the first
            // time has to match one born above it, and OnWoundAdded leaves ScalingLimit alone;
            // bumping it here too would just move the same-severity gap to the scaling side.
            if (!crossedFromBelow)
                component.ScalingLimit += 0.6;

            component.IsBleeding = true;
        }

        // dummy fix as me and pretty much nobody else currently knows HOW EXACTLY was is supposed to work, womp womp
        // seems to work fine though so why not
        if (component.BleedingAmountRaw > 0) // Goobstation
        {
            component.Scaling = 1;
        }

        Dirty(uid, component);
    }

    public void OnBleedRemoverSeverityUpdate(EntityUid uid, BleedRemoverComponent component, ref WoundSeverityPointChangedEvent args)
    {
        var delta = args.NewSeverity - args.OldSeverity;
        if (delta < component.SeverityThreshold
            || !TryComp(uid, out WoundComponent? wound)
            || TerminatingOrDeleted(wound.HoldingWoundable)
            || !TryComp(wound.HoldingWoundable, out WoundableComponent? woundable)
            || !TryComp(wound.HoldingWoundable, out OrganComponent? organ)
            || !organ.Body.HasValue)
            return;

        var result = _wound.TryHealBleedingWounds(wound.HoldingWoundable,
            (-delta * component.BleedingRemovalMultiplier).Float(),
            out var _,
            woundable);

        if (!result)
            return;

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/lightburn.ogg"), organ.Body.Value);
        _popup.PopupPredicted(Loc.GetString("bloodstream-component-wounds-cauterized"),
            organ.Body.Value,
            organ.Body.Value,
            PopupType.Medium);
    }

    public bool TryHealWoundBleeding(EntityUid mob, FixedPoint2 amount, BloodstreamComponent? bloodstream = null)
    {
        if (!Resolve(mob, ref bloodstream, logMissing: false) || amount <= FixedPoint2.Zero)
            return false;

        if (!TryComp<BodyComponent>(mob, out var body) || body.Organs is null)
            return TryModifyBleedAmount((mob, bloodstream), -amount.Float());

        var healedAny = false;
        foreach (var organ in body.Organs.ContainedEntities)
        {
            if (TryComp<WoundableComponent>(organ, out var woundable) && _wound.TryHealBleedingWounds(organ, amount, out _, woundable))
                healedAny = true;
        }

        return healedAny;
    }

    // begin Goobstation: port EE height/width sliders
    public void SetBloodMaxVolume(Entity<BloodstreamComponent?> ent, FixedPoint2 volume)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return;

        ent.Comp.BloodReferenceSolution.Volume = volume;
    }
    // end Goobstation: port EE height/width sliders
}
