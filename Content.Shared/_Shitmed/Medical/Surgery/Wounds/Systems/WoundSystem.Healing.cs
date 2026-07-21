using System.Linq;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;

namespace Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;

public sealed partial class WoundSystem
{
    private void ProcessHealing(Entity<WoundableComponent> ent)
    {
        if (ent.Comp.CanHealBleeds)
            TryHealBleedingWounds(ent, ent.Comp.BleedingTreatmentAbility, out _, ent.Comp);

        if (ent.Comp.CanHealDamage
            && TryHealWoundsOnWoundable(ent, ent.Comp.HealAbility, out _, out var healedByType, ent.Comp)
            && !healedByType.Empty)
        {
            var negated = -healedByType;

            // The wound severity above is already healed - suppress OnDamageDealt's own
            // negative-delta reaction to this follow-up call so HealWoundsCore doesn't run a
            // second time on the same wounds (see _suppressWoundInduction's own doc comment).
            // The mob's own DamageableComponent follows automatically via
            // BodyDamageBridgeSystem's organ->mob sync - no separate mirror write needed here.
            _suppressWoundInduction = true;
            _damageable.TryChangeDamage(ent.Owner, negated, ignoreResistances: true, interruptsDoAfters: false, origin: null);
            _suppressWoundInduction = false;
        }
    }

    public bool TryHaltAllBleeding(EntityUid woundable, WoundableComponent? component = null, bool force = false)
    {
        if (!Resolve(woundable, ref component) || (component.Bleeds <= FixedPoint2.Zero && !force))
            return false;

        var haltedAny = false;
        foreach (var wound in GetWoundableWounds(woundable, component).ToList())
        {
            if (!TryComp<BleedInflicterComponent>(wound, out var bleeds))
                continue;

            if (!force && bleeds.BleedingAmountRaw <= FixedPoint2.Zero && !bleeds.IsBleeding)
                continue;

            bleeds.BleedingAmountRaw = FixedPoint2.Zero;
            bleeds.Scaling = FixedPoint2.Zero;
            bleeds.IsBleeding = false;
            Dirty(wound, bleeds);
            haltedAny = true;
        }

        RecomputeWoundableBleeds(woundable, component);
        return haltedAny;
    }

    public bool TryHealBleedingWounds(EntityUid woundable, FixedPoint2 bleedStopAbility, out FixedPoint2 healed, WoundableComponent? component = null)
    {
        healed = FixedPoint2.Zero;

        if (!Resolve(woundable, ref component) || component.Bleeds <= FixedPoint2.Zero || bleedStopAbility <= FixedPoint2.Zero)
            return false;

        foreach (var wound in GetWoundableWounds(woundable, component).ToList())
        {
            if (!TryComp<BleedInflicterComponent>(wound, out var bleeds) || !bleeds.IsBleeding)
                continue;

            if (bleedStopAbility >= bleeds.BleedingAmount)
            {
                healed += bleeds.BleedingAmount;
                bleeds.BleedingAmountRaw = FixedPoint2.Zero;
                bleeds.IsBleeding = false;
                bleeds.Scaling = FixedPoint2.Zero;
            }
            else
            {
                bleeds.BleedingAmountRaw -= bleedStopAbility;
                healed += bleedStopAbility;
            }

            Dirty(wound, bleeds);
        }

        RecomputeWoundableBleeds(woundable, component);
        return healed > FixedPoint2.Zero;
    }

    public void ForceHealWoundsOnWoundable(EntityUid woundable, out FixedPoint2 healed, WoundableComponent? component = null)
    {
        healed = FixedPoint2.Zero;

        if (!Resolve(woundable, ref component))
            return;

        foreach (var wound in GetWoundableWounds(woundable, component).ToList())
        {
            healed += wound.Comp.WoundSeverityPoint;
            SetWoundSeverity(wound, FixedPoint2.Zero, wound.Comp);
        }

        UpdateWoundableIntegrity(woundable, component);
        CheckWoundableSeverityThresholds(woundable, component);
    }

    /// <summary>
    /// Heals up to <paramref name="healAmount"/> worth of severity, distributed across the
    /// woundable's active (non-scar) wounds, most-recently-induced first.
    /// </summary>
    public bool TryHealWoundsOnWoundable(EntityUid woundable, FixedPoint2 healAmount, out FixedPoint2 healed, WoundableComponent? component = null, bool ignoreBlockers = false)
    {
        return TryHealWoundsOnWoundable(woundable, healAmount, out healed, out _, component, ignoreBlockers);
    }

    /// <summary>
    /// Same as the other overload, but also reports which damage types the healed severity came
    /// from and how much of each - added so ProcessHealing can mirror the exact amount actually
    /// healed onto the organ's/mob's own raw DamageableComponent, the same way
    /// HealingSystem.OnDoAfter's item-healing already does (see GetHealableSeverity's own doc
    /// comment for the "raw pool desynced from wound state" bug class this avoids).
    /// </summary>
    public bool TryHealWoundsOnWoundable(EntityUid woundable, FixedPoint2 healAmount, out FixedPoint2 healed, out DamageSpecifier healedByType, WoundableComponent? component = null, bool ignoreBlockers = false)
    {
        healed = FixedPoint2.Zero;
        healedByType = new DamageSpecifier();

        if (!Resolve(woundable, ref component) || healAmount <= FixedPoint2.Zero)
            return false;

        var remaining = healAmount;
        // Snapshotted: SetWoundSeverity below can heal a wound down to Healed and remove it
        // from this same Wounds container mid-loop (CheckSeverityThresholds -> RemoveWound),
        // which corrupts a live enumeration of that container. Matches ForceHealWoundsOnWoundable's
        // own .ToList() a few lines up, for the same reason.
        foreach (var wound in GetWoundableWounds(woundable, component).ToList())
        {
            if (remaining <= FixedPoint2.Zero)
                break;

            if (!CanHealWound(wound, wound.Comp, ignoreBlockers))
                continue;

            var heal = FixedPoint2.Min(remaining, wound.Comp.WoundSeverityPoint);
            SetWoundSeverity(wound, wound.Comp.WoundSeverityPoint - heal, wound.Comp);
            remaining -= heal;
            healed += heal;

            healedByType.DamageDict[wound.Comp.DamageType] =
                healedByType.DamageDict.GetValueOrDefault(wound.Comp.DamageType) + heal;
        }

        if (healed <= FixedPoint2.Zero)
            return false;

        UpdateWoundableIntegrity(woundable, component);
        CheckWoundableSeverityThresholds(woundable, component);
        return true;
    }

    public FixedPoint2 GetHealableSeverity(EntityUid woundable, string damageType, FixedPoint2 healAmount, WoundableComponent? component = null, bool ignoreBlockers = false)
    {
        if (!Resolve(woundable, ref component) || healAmount <= FixedPoint2.Zero)
            return FixedPoint2.Zero;

        var remaining = healAmount;
        var healed = FixedPoint2.Zero;
        foreach (var wound in GetWoundableWounds(woundable, component))
        {
            if (remaining <= FixedPoint2.Zero)
                break;

            if (wound.Comp.DamageType != damageType || !CanHealWound(wound, wound.Comp, ignoreBlockers))
                continue;

            var heal = FixedPoint2.Min(remaining, wound.Comp.WoundSeverityPoint);
            remaining -= heal;
            healed += heal;
        }

        return healed;
    }

    private void HealWoundsCore(EntityUid woundable, FixedPoint2 healAmount, string damageType, out FixedPoint2 healed, WoundableComponent? component = null, bool ignoreBlockers = false)
    {
        healed = FixedPoint2.Zero;

        if (!Resolve(woundable, ref component) || healAmount <= FixedPoint2.Zero)
            return;

        var remaining = healAmount;
        // Snapshotted - see TryHealWoundsOnWoundable's identical comment above.
        foreach (var wound in GetWoundableWounds(woundable, component).ToList())
        {
            if (remaining <= FixedPoint2.Zero)
                break;

            if (wound.Comp.DamageType != damageType || !CanHealWound(wound, wound.Comp, ignoreBlockers))
                continue;

            var heal = FixedPoint2.Min(remaining, wound.Comp.WoundSeverityPoint);
            SetWoundSeverity(wound, wound.Comp.WoundSeverityPoint - heal, wound.Comp);
            remaining -= heal;
            healed += heal;
        }

        if (healed <= FixedPoint2.Zero)
            return;

        UpdateWoundableIntegrity(woundable, component);
        CheckWoundableSeverityThresholds(woundable, component);
    }

    public bool HasDamageOfType(EntityUid woundable, string damageType, WoundableComponent? component = null)
    {
        return GetWoundableWounds(woundable, component).Any(w => w.Comp.DamageType == damageType && !w.Comp.IsScar);
    }

    public bool CanHealWound(EntityUid wound, WoundComponent? comp = null, bool ignoreBlockers = false)
    {
        if (!Resolve(wound, ref comp) || !comp.CanBeHealed || comp.IsScar)
            return false;

        if (ignoreBlockers)
            return true;

        var attempt = new WoundHealAttemptEvent((comp.HoldingWoundable, Comp<WoundableComponent>(comp.HoldingWoundable)), ignoreBlockers);
        RaiseLocalEvent(wound, ref attempt);

        return !attempt.Cancelled;
    }

    /// <summary>
    /// All wound entities across every woundable limb-organ belonging to the given mob.
    /// </summary>
    public bool TryGetAllOwnerWounds(EntityUid mob, out List<Entity<WoundComponent>> wounds)
    {
        wounds = new List<Entity<WoundComponent>>();

        if (!TryComp<BodyComponent>(mob, out var body) || body.Organs is null)
            return false;

        foreach (var organ in body.Organs.ContainedEntities)
        {
            if (!HasComp<WoundableComponent>(organ))
                continue;

            wounds.AddRange(GetWoundableWounds(organ));
        }

        return wounds.Count > 0;
    }

    /// <summary>
    /// All woundable limb-organs belonging to the given mob that currently have wounds.
    /// </summary>
    public bool TryGetAllOwnerWoundedParts(EntityUid mob, out List<Entity<WoundableComponent>> woundables)
    {
        woundables = new List<Entity<WoundableComponent>>();

        if (!TryComp<BodyComponent>(mob, out var body) || body.Organs is null)
            return false;

        foreach (var organ in body.Organs.ContainedEntities)
        {
            if (TryComp<WoundableComponent>(organ, out var woundable) && woundable.WoundableIntegrity < woundable.IntegrityCap)
                woundables.Add((organ, woundable));
        }

        return woundables.Count > 0;
    }
}
