// SPDX-FileCopyrightText: 2026 Maciej Walendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._Shitmed.Body;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;

public sealed partial class WoundSystem
{
    /// <summary>
    /// Every wound on the given entity — if it's a mob (has BodyComponent), aggregates
    /// across every woundable limb-organ it owns; if it's a woundable organ directly,
    /// just that one's wounds.
    /// </summary>
    public IEnumerable<Entity<WoundComponent>> GetAllWounds(EntityUid targetEntity)
    {
        if (TryComp<BodyComponent>(targetEntity, out var body) && body.Organs is not null)
        {
            foreach (var organ in body.Organs.ContainedEntities)
            {
                if (!HasComp<WoundableComponent>(organ))
                    continue;

                foreach (var wound in GetWoundableWounds(organ))
                    yield return wound;
            }

            yield break;
        }

        if (HasComp<WoundableComponent>(targetEntity))
        {
            foreach (var wound in GetWoundableWounds(targetEntity))
                yield return wound;
        }
    }

    /// <summary>
    /// Every woundable limb-organ belonging to the given mob.
    /// </summary>
    public IEnumerable<Entity<WoundableComponent>> GetAllWoundableChildren(EntityUid mob)
    {
        if (!TryComp<BodyComponent>(mob, out var body) || body.Organs is null)
            yield break;

        foreach (var organ in body.Organs.ContainedEntities)
        {
            if (TryComp<WoundableComponent>(organ, out var woundable))
                yield return (organ, woundable);
        }
    }

    public IEnumerable<Entity<WoundComponent>> GetWoundableWounds(EntityUid woundable, WoundableComponent? component = null)
    {
        if (!Resolve(woundable, ref component) || component.Wounds is null)
            yield break;

        foreach (var contained in component.Wounds!.ContainedEntities)
        {
            if (TryComp<WoundComponent>(contained, out var woundComp))
                yield return (contained, woundComp);
        }
    }

    /// <summary>
    /// Sum of wound severities on this woundable. <paramref name="damageGroup"/> filters to
    /// wounds of that damage group only (e.g. for tend-wounds steps that only heal Brute);
    /// <paramref name="healable"/> additionally filters to wounds CanHealWound accepts.
    /// </summary>
    public FixedPoint2 GetWoundableSeverityPoint(
        EntityUid woundable,
        WoundableComponent? component = null,
        ProtoId<DamageGroupPrototype>? damageGroup = null,
        bool healable = false)
    {
        var total = FixedPoint2.Zero;
        foreach (var wound in GetWoundableWounds(woundable, component))
        {
            if (wound.Comp.IsScar)
                continue;

            if (damageGroup is { } group && wound.Comp.DamageGroup != group)
                continue;

            if (healable && !CanHealWound(wound, wound.Comp))
                continue;

            total += wound.Comp.WoundSeverityPoint;
        }

        return total;
    }

    public FixedPoint2 GetWoundableIntegrityDamage(EntityUid woundable, WoundableComponent? component = null)
    {
        return GetWoundableSeverityPoint(woundable, component);
    }

    /// <summary>
    /// Whether any non-scar wound of the given damage group is present on this woundable.
    /// </summary>
    public bool HasDamageOfGroup(EntityUid woundable, ProtoId<DamageGroupPrototype> damageGroup, WoundableComponent? component = null)
    {
        return GetWoundableWounds(woundable, component).Any(w => !w.Comp.IsScar && w.Comp.DamageGroup == damageGroup);
    }

    /// <summary>
    /// Raw DamageableComponent total for a damage group on this woundable, independent of
    /// wound entities. Damage below TryCreateWound's minorThreshold (scaled by IntegrityCap,
    /// so it hits large organs like the torso far harder than small ones) never spawns a
    /// wound at all, but it still accumulates on the organ - without this, that damage was
    /// both invisible to tend-wound surgeries and permanently unhealable.
    /// </summary>
    public FixedPoint2 GetGroupDamage(EntityUid woundable, ProtoId<DamageGroupPrototype> damageGroup, DamageableComponent? damageable = null)
    {
        if (!Resolve(woundable, ref damageable))
            return FixedPoint2.Zero;

        var total = FixedPoint2.Zero;
        foreach (var value in _damageable.GetPositiveDamage((woundable, damageable), damageGroup).DamageDict.Values)
            total += value;

        return total;
    }

    /// <summary>
    /// Raw DamageableComponent amount of a single damage type on this woundable, independent of
    /// wound entities - the single-type sibling of <see cref="GetGroupDamage"/>. Used by
    /// HealingSystem.OnDoAfter to tell "this organ has real but wound-less (sub-threshold)
    /// damage of this type" apart from "this type never lands on organs at all" (e.g.
    /// Barotrauma/Temperature) - the two cases used to be conflated, silently routing a
    /// wound-less organ's heal onto the mob's aggregate pool instead, which in practice healed
    /// whichever OTHER organ actually held a wound of that type.
    /// </summary>
    public FixedPoint2 GetTypeDamage(EntityUid woundable, string damageType, DamageableComponent? damageable = null)
    {
        if (!Resolve(woundable, ref damageable))
            return FixedPoint2.Zero;

        return _damageable.GetPositiveDamage((woundable, damageable)).DamageDict.GetValueOrDefault(damageType);
    }

    public FixedPoint2 GetOrganlessDamage(EntityUid mob, string damageType)
    {
        var mobDamage = GetTypeDamage(mob, damageType);

        if (!TryComp<BodyComponent>(mob, out var body) || body.Organs is null)
            return mobDamage;

        var organDamage = FixedPoint2.Zero;
        foreach (var organ in body.Organs.ContainedEntities)
        {
            if (HasComp<WoundableComponent>(organ))
                organDamage += GetTypeDamage(organ, damageType);
        }

        return FixedPoint2.Max(mobDamage - organDamage, FixedPoint2.Zero);
    }

    public bool HasWoundsExceedingMangleSeverity(EntityUid woundable, WoundableComponent? component = null)
    {
        return GetWoundableWounds(woundable, component)
            .Any(w => w.Comp.MangleSeverity is { } mangle && w.Comp.WoundSeverity >= mangle);
    }

    /// <summary>
    /// Every valid target slot on the mob, mapped to its limb-organ's WoundableSeverity
    /// (Severed for slots with no organ present at all).
    /// </summary>
    public Dictionary<TargetBodyPart, WoundableSeverity> GetWoundableStatesOnBody(EntityUid mob)
    {
        var result = new Dictionary<TargetBodyPart, WoundableSeverity>();

        foreach (var part in SharedTargetingSystem.GetValidParts())
        {
            result[part] = WoundableSeverity.Severed;
        }

        if (!TryComp<BodyComponent>(mob, out var body) || body.Organs is null)
            return result;

        foreach (var organ in body.Organs.ContainedEntities)
        {
            if (!TryComp<OrganComponent>(organ, out var organComp)
                || organComp.Category is not { } category
                || !LimbTargetMap.TryGetTarget(category, out var target)
                || !TryComp<WoundableComponent>(organ, out var woundable))
                continue;

            result[target] = woundable.WoundableSeverity;
        }

        return result;
    }

    /// <summary>
    /// Same as <see cref="GetWoundableStatesOnBody"/> but bucketed from each organ's own
    /// raw DamageableComponent total rather than derived WoundableIntegrity.
    /// </summary>
    public Dictionary<TargetBodyPart, WoundableSeverity> GetDamageableStatesOnBody(EntityUid mob)
    {
        var result = new Dictionary<TargetBodyPart, WoundableSeverity>();

        foreach (var part in SharedTargetingSystem.GetValidParts())
        {
            result[part] = WoundableSeverity.Severed;
        }

        if (!TryComp<BodyComponent>(mob, out var body) || body.Organs is null)
            return result;

        foreach (var organ in body.Organs.ContainedEntities)
        {
            if (!TryComp<OrganComponent>(organ, out var organComp)
                || organComp.Category is not { } category
                || !LimbTargetMap.TryGetTarget(category, out var target)
                || !TryComp<WoundableComponent>(organ, out var woundable))
                continue;

            var totalDamage = _damageable.GetTotalDamage(organ);
            var severity = WoundableSeverity.Healthy;
            if (woundable.SortedThresholds is { } sorted)
            {
                var remaining = woundable.IntegrityCap - totalDamage;
                foreach (var (candidate, threshold) in sorted)
                {
                    if (candidate == WoundableSeverity.Severed)
                        continue;

                    if (remaining <= threshold)
                        severity = candidate;
                }
            }

            result[target] = severity;
        }

        return result;
    }

    /// <summary>
    /// Fallback organ to redirect further damage to when <paramref name="exhausted"/>'s
    /// integrity is already at zero — walks one step up LimbTargetMap's static hierarchy
    /// (hand -&gt; arm -&gt; torso).
    /// </summary>
    public EntityUid? GetDamageRedirectTarget(EntityUid mob, EntityUid exhausted, string damageType)
    {
        if (!TryComp<OrganComponent>(exhausted, out var organComp) || organComp.Category is not { } category)
            return null;

        if (!LimbTargetMap.TryGetParentCategory(category, out var parentCategory))
            return null;

        if (!TryComp<BodyComponent>(mob, out var body))
            return null;

        return LimbTargetMap.TryGetOrganByCategory(EntityManager, body, parentCategory, out var parentOrgan)
            ? parentOrgan
            : null;
    }

    /// <summary>
    /// Whether this organ is the hub of the limb hierarchy (Torso).
    /// </summary>
    public bool IsWoundableRoot(EntityUid woundable)
    {
        return TryComp<OrganComponent>(woundable, out var organ) && organ.Category == "Torso";
    }

    /// <summary>
    /// Continues an existing non-scar wound of the same damage type if one exists,
    /// otherwise creates a new wound entity via the "Wound{DamageType}" prototype (e.g.
    /// "WoundBlunt").
    /// </summary>
    public bool TryInduceWound(Entity<WoundableComponent> woundable, string damageType, FixedPoint2 severity, out Entity<WoundComponent>? woundInduced, WoundableComponent? component = null, bool bypassMinimumSeverity = false)
    {
        component ??= woundable.Comp;

        if (TryContinueWound(woundable, damageType, severity, out woundInduced, component))
            return true;

        return TryCreateWound(woundable, $"Wound{damageType}", severity, out woundInduced, component, bypassMinimumSeverity);
    }

    public bool TryContinueWound(EntityUid woundable, string damageType, FixedPoint2 severity, out Entity<WoundComponent>? woundContinued, WoundableComponent? component = null)
    {
        woundContinued = null;

        foreach (var wound in GetWoundableWounds(woundable, component))
        {
            if (wound.Comp.IsScar || wound.Comp.DamageType != damageType)
                continue;

            ApplyWoundSeverity(wound, severity, wound.Comp);
            woundContinued = wound;
            return true;
        }

        return false;
    }

    public bool TryCreateWound(EntityUid woundable, string woundProtoId, FixedPoint2 severity, out Entity<WoundComponent>? woundCreated, WoundableComponent? component = null, bool bypassMinimumSeverity = false)
    {
        woundCreated = null;

        if (!Resolve(woundable, ref component) || !IsWoundPrototypeValid(woundProtoId))
            return false;

        // Guard against creating a wound that would be born already below the lowest real
        // severity bucket (WoundThresholds[Minor], scaled by this woundable's IntegrityCap) -
        // CheckSeverityThresholds would immediately flag it WoundSeverity.Healed and call
        // RemoveWound on it, but the wound hasn't been inserted into woundable.Wounds yet at
        // that point (SetWoundSeverity runs before AddWound below, deliberately, so
        // OnWoundInserted sees the real severity at insertion time) - RemoveWound's container
        // Remove would crash with "entity that was never inside of the container." Negligible
        // damage (e.g. one limb's share of a heavily-split environmental tick) just doesn't
        // produce a wound at all, matching what CheckSeverityThresholds already treats as
        // "no wound" for existing wounds that heal back down past this same threshold.
        var minorThreshold = WoundThresholds[WoundSeverity.Minor] * (component.IntegrityCap / FixedPoint2.New(100));
        if (!bypassMinimumSeverity && severity < minorThreshold)
            return false;

        var wound = EntityManager.PredictedSpawn(woundProtoId);
        var woundComp = Comp<WoundComponent>(wound);

        woundComp.DamageGroup = GetDamageGroupByType(woundComp.DamageType)?.ID;

        // HoldingWoundable must be set before SetWoundSeverity: CheckSeverityThresholds
        // (called from SetWoundSeverity) resolves the woundable's IntegrityCap through it
        // to scale the wound's own severity bucket. And severity must be set before
        // inserting into the container: OnWoundInserted recomputes WoundableIntegrity off
        // whatever severity the wound already has at insertion time.
        woundComp.HoldingWoundable = woundable;
        SetWoundSeverity(wound, severity, woundComp);
        AddWound(woundable, wound, component, woundComp);

        var addedEv = new WoundAddedEvent(woundComp, component);
        RaiseLocalEvent(wound, ref addedEv);

        woundCreated = (wound, woundComp);
        return true;
    }

    private void AddWound(EntityUid woundableUid, EntityUid wound, WoundableComponent woundable, WoundComponent woundComp)
    {
        woundComp.HoldingWoundable = woundableUid;
        _container.Insert(wound, woundable.Wounds!);
        UpdateWoundableAppearance(woundableUid, woundable);
    }

    /// <summary>
    /// Pushes this woundable's current wound list into its own AppearanceComponent, read
    /// client-side by WoundableVisualsSystem to render damage/bleed sprite overlays.
    /// </summary>
    private void UpdateWoundableAppearance(EntityUid woundableUid, WoundableComponent? woundable = null)
    {
        if (!Resolve(woundableUid, ref woundable) || woundable.Wounds is null)
            return;

        var woundList = new List<NetEntity>(woundable.Wounds.ContainedEntities.Count);
        foreach (var wound in woundable.Wounds.ContainedEntities)
            woundList.Add(GetNetEntity(wound));

        _appearance.SetData(woundableUid, WoundableVisualizerKeys.Wounds, new WoundVisualizerGroupData(woundList));
    }

    private bool IsWoundPrototypeValid(string protoId)
    {
        return _proto.TryIndex<EntityPrototype>(protoId, out var proto) && proto.Components.ContainsKey("Wound");
    }
}
