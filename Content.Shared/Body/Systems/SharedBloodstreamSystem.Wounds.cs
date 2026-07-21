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
        if (!CanWoundBleed(uid, component)
            || args.Component.WoundSeverityPoint < component.SeverityThreshold
            || !args.Woundable.CanBleed)
            return;

        // wounds that BLEED will not HEAL.
        component.BleedingAmountRaw = args.Component.WoundSeverityPoint * _cfg.GetCVar(SurgeryCVars.BleedingSeverityTrade);

        var formula = (float) (args.Component.WoundSeverityPoint / _cfg.GetCVar(SurgeryCVars.BleedsScalingTime) * component.ScalingSpeed);
        component.ScalingFinishesAt = _timing.CurTime + TimeSpan.FromSeconds(formula);
        component.ScalingStartsAt = _timing.CurTime;
        component.IsBleeding = true;

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
        if (!CanWoundBleed(uid, component)
            || !TryComp<WoundableComponent>(args.Component.HoldingWoundable, out var woundable)
            || !woundable.CanBleed
            || args.NewSeverity < component.SeverityThreshold
            || args.NewSeverity < args.OldSeverity)
            return;

        var oldBleedsAmount = args.OldSeverity * _cfg.GetCVar(SurgeryCVars.BleedingSeverityTrade);
        component.BleedingAmountRaw = args.NewSeverity * _cfg.GetCVar(SurgeryCVars.BleedingSeverityTrade);

        var severityPenalty = component.BleedingAmountRaw - oldBleedsAmount / _cfg.GetCVar(SurgeryCVars.BleedsScalingTime);
        component.SeverityPenalty += severityPenalty;

        var formula = (float) (args.NewSeverity / _cfg.GetCVar(SurgeryCVars.BleedsScalingTime) * component.ScalingSpeed);
        component.ScalingFinishesAt = _timing.CurTime + TimeSpan.FromSeconds(formula);
        component.ScalingStartsAt = _timing.CurTime;

        if (!component.IsBleeding)
        {
            component.ScalingLimit += 0.6;
            component.IsBleeding = true;
            // When bleeding is reopened, the severity is increased
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
