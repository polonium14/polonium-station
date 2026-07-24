using System.Linq;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Targeting;
using Content.Shared._Shitmed.Targeting.Events;
using Content.Shared.Body;
using Content.Shared.FixedPoint;

namespace Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;

public sealed partial class WoundSystem
{
    [Dependency] private TraumaSystem _trauma = default!;

    /// <summary>
    /// Directly sets a wound's severity point value and re-derives its WoundSeverity bucket.
    /// </summary>
    public void SetWoundSeverity(EntityUid uid, FixedPoint2 severity, WoundComponent? wound = null)
    {
        if (!Resolve(uid, ref wound))
            return;

        var old = wound.WoundSeverityPoint;
        var newSeverity = FixedPoint2.Max(FixedPoint2.Zero, severity);

        if (old == newSeverity)
            return;

        wound.WoundSeverityPoint = newSeverity;
        Dirty(uid, wound);

        var evt = new WoundSeverityPointChangedEvent(uid, wound, old, newSeverity);
        RaiseLocalEvent(uid, ref evt);

        CheckSeverityThresholds(uid, wound);
    }

    /// <summary>
    /// Applies a delta to a wound's severity. Does NOT call UpdateWoundableIntegrity or
    /// CheckWoundableSeverityThresholds — callers must do so themselves once done batching.
    /// </summary>
    public void ApplyWoundSeverity(EntityUid uid, FixedPoint2 severity, WoundComponent? wound = null)
    {
        if (!Resolve(uid, ref wound))
            return;

        if (severity > 0
            && wound.MangleSeverity != null
            && HasWoundsExceedingMangleSeverity(wound.HoldingWoundable))
            _trauma.ApplyMangledTraumas(wound.HoldingWoundable, uid, severity);

        SetWoundSeverity(uid, wound.WoundSeverityPoint + severity, wound);
    }

    public FixedPoint2 ApplySeverityModifiers(EntityUid woundable, FixedPoint2 severity, WoundableComponent? component = null)
    {
        if (!Resolve(woundable, ref component) || component.SeverityMultipliers.Count == 0)
            return severity;

        foreach (var multiplier in component.SeverityMultipliers.Values)
        {
            severity *= multiplier;
        }

        return severity;
    }

    public bool TryAddWoundableSeverityMultiplier(EntityUid uid, string identifier, FixedPoint2 change, WoundableComponent? component = null)
    {
        if (!Resolve(uid, ref component) || component.SeverityMultipliers.ContainsKey(identifier))
            return false;

        component.SeverityMultipliers[identifier] = change;
        Dirty(uid, component);
        return true;
    }

    public bool TryRemoveWoundableSeverityMultiplier(EntityUid uid, string identifier, WoundableComponent? component = null)
    {
        if (!Resolve(uid, ref component) || !component.SeverityMultipliers.Remove(identifier))
            return false;

        Dirty(uid, component);
        return true;
    }

    public bool TryChangeWoundableSeverityMultiplier(EntityUid uid, string identifier, FixedPoint2 change, WoundableComponent? component = null)
    {
        if (!Resolve(uid, ref component) || !component.SeverityMultipliers.ContainsKey(identifier))
            return false;

        component.SeverityMultipliers[identifier] = change;
        Dirty(uid, component);
        return true;
    }

    /// <summary>
    /// Recomputes WoundableIntegrity by summing every non-scar wound's severity in the
    /// Wounds container, clamped to [0, IntegrityCap].
    /// </summary>
    public void UpdateWoundableIntegrity(EntityUid uid, WoundableComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var total = FixedPoint2.Zero;
        foreach (var wound in GetWoundableWounds(uid, component))
        {
            if (wound.Comp.IsScar)
                continue;

            total += wound.Comp.WoundIntegrityDamage;
        }

        component.WoundableIntegrity = FixedPoint2.Clamp(component.IntegrityCap - total, FixedPoint2.Zero, component.IntegrityCap);
        Dirty(uid, component);
    }

    /// <summary>
    /// Public wrapper around UpdateWoundableIntegrity + the otherwise-private
    /// CheckWoundableSeverityThresholds, for callers outside WoundSystem that mutate a
    /// woundable's attachment state directly (e.g. Surgery's reattach-limb step, which
    /// container-inserts a limb-organ back onto a body without going through SetWoundSeverity
    /// — its WoundableSeverity is still whatever it was when it got amputated, usually
    /// Severed, and needs re-deriving from its current wounds now that it's live again).
    /// </summary>
    public void RecomputeWoundableSeverity(EntityUid woundable, WoundableComponent? component = null)
    {
        if (!Resolve(woundable, ref component))
            return;

        UpdateWoundableIntegrity(woundable, component);
        CheckWoundableSeverityThresholds(woundable, component);
    }

    /// <summary>
    /// Recomputes WoundableComponent.Bleeds by summing BleedingAmount across every currently-
    /// bleeding wound in this woundable's Wounds container. Called every tick from
    /// SharedBloodstreamSystem's Update loop rather than owned by BloodstreamSystem directly,
    /// since WoundableComponent's write access is scoped to this system.
    /// </summary>
    public void RecomputeWoundableBleeds(EntityUid woundable, WoundableComponent? component = null)
    {
        if (!Resolve(woundable, ref component))
            return;

        var total = FixedPoint2.Zero;
        foreach (var wound in GetWoundableWounds(woundable, component))
        {
            if (TryComp<BleedInflicterComponent>(wound, out var bleeds) && bleeds.IsBleeding)
                total += bleeds.BleedingAmount;
        }

        if (component.Bleeds == total)
            return;

        component.Bleeds = total;
        Dirty(woundable, component);
    }

    private void CheckSeverityThresholds(EntityUid wound, WoundComponent component)
    {
        var old = component.WoundSeverity;
        var severity = WoundSeverity.Healed;

        var integrityCap = TryComp<WoundableComponent>(component.HoldingWoundable, out var woundable)
            ? woundable.IntegrityCap
            : FixedPoint2.New(100);

        foreach (var (candidate, threshold) in WoundThresholds.OrderByDescending(kv => kv.Value))
        {
            var scaled = threshold * (integrityCap / FixedPoint2.New(100));
            if (component.WoundSeverityPoint >= scaled)
            {
                severity = candidate;
                break;
            }
        }

        if (severity == old)
            return;

        component.WoundSeverity = severity;
        Dirty(wound, component);

        if (severity == WoundSeverity.Healed)
            RemoveWound(wound, component);
    }

    /// <summary>
    /// Removes a fully-healed wound from its woundable's Wounds container and deletes it,
    /// after notifying subscribers (Pain, Traumas) via WoundRemovedEvent.
    /// </summary>
    private void RemoveWound(EntityUid wound, WoundComponent component)
    {
        var evt = new WoundRemovedEvent(wound, component);
        RaiseLocalEvent(wound, ref evt);

        if (TryComp<WoundableComponent>(component.HoldingWoundable, out var woundable) && woundable.Wounds is not null)
        {
            // Capture the woundable uid before Remove, not after: without reparent:false, a
            // container Remove re-parents the now-orphaned wound entity up into whatever
            // container its OWN parent (the woundable organ) sits in - the mob's body_organs
            // container - which fires OnWoundInserted for that container and overwrites
            // WoundComponent.HoldingWoundable to point at the mob instead of the organ. Harmless
            // before this method had a reason to re-read the field (the wound gets QueueDel'd
            // moments later anyway), but UpdateWoundableAppearance below needs the real organ uid.
            var woundableUid = component.HoldingWoundable;
            _container.Remove(wound, woundable.Wounds, force: true, reparent: false);
            UpdateWoundableAppearance(woundableUid, woundable);
        }

        QueueDel(wound);
    }

    private void CheckWoundableSeverityThresholds(EntityUid woundable, WoundableComponent? component = null)
    {
        if (!Resolve(woundable, ref component))
            return;

        var old = component.WoundableSeverity;

        if (component.WoundableIntegrity >= component.IntegrityCap)
        {
            component.WoundableSeverity = WoundableSeverity.Healthy;
        }
        else if (component.SortedThresholds is { } sorted)
        {
            foreach (var (candidate, threshold) in sorted)
            {
                if (candidate == WoundableSeverity.Severed)
                    continue;

                if (component.WoundableIntegrity <= threshold)
                {
                    component.WoundableSeverity = candidate;
                }
            }
        }

        if (component.WoundableSeverity == old)
            return;

        Dirty(woundable, component);

        var evt = new WoundableSeverityChangedEvent(woundable, old, component.WoundableSeverity);
        RaiseLocalEvent(woundable, ref evt);

        SyncTargetingBodyStatus(woundable);
    }

    /// <summary>
    /// Pushes this limb's severity into the owning mob's TargetingComponent.BodyStatus and
    /// notifies the client to refresh the targeting/health-analyzer UI. Uses
    /// GetDamageableStatesOnBody - the same proc the health analyzer's doll reads - so the
    /// PartStatus doll HUD widget always matches the analyzer.
    /// </summary>
    private void SyncTargetingBodyStatus(EntityUid woundable)
    {
        if (!TryComp<OrganComponent>(woundable, out var organ) || organ.Body is not { } body)
            return;

        if (!TryComp<TargetingComponent>(body, out var targeting))
            return;

        targeting.BodyStatus = GetDamageableStatesOnBody(body);
        Dirty(body, targeting);

        RaiseNetworkEvent(new TargetIntegrityChangeEvent(GetNetEntity(body)), body);
    }
}
