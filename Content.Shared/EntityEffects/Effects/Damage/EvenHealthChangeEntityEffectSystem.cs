// SPDX-FileCopyrightText: 2025 Hannah Giovanna Dawson <karakkaraz@gmail.com>
// SPDX-FileCopyrightText: 2025 PJB3005 <pieterjan.briers+git@gmail.com>
// SPDX-FileCopyrightText: 2025 Princess Cheeseballs <66055347+Princess-Cheeseballs@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Samuka <47865393+Samuka-C@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Vasilis The Pikachu <vasilis@pikachu.systems>
// SPDX-FileCopyrightText: 2026 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 taydeo <tay@funkystation.org>
// SPDX-FileCopyrightText: 2026 taydeo <td12233a@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Shared._Shitmed.Body;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Localizations;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects.Effects.Damage;

/// <summary>
/// Evenly heal the damage types in a damage group by up to a specified total on this entity.
/// Total adjustment is modified by scale.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class EvenHealthChangeEntityEffectSystem : EntityEffectSystem<DamageableComponent, EvenHealthChange>
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private WoundSystem _wound = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    protected override void Effect(Entity<DamageableComponent> entity, ref EntityEffectEvent<EvenHealthChange> args)
    {
        foreach (var (group, amount) in args.Effect.Damage)
        {
            HealGroup(entity, amount * args.Scale, group);
        }
    }

    private void HealGroup(Entity<DamageableComponent> entity, FixedPoint2 amount, ProtoId<DamageGroupPrototype> group)
    {
        if (amount >= 0
            || !_prototype.TryIndex(group, out var groupProto)
            || !TryComp<BodyComponent>(entity, out var body)
            || body.Organs is null)
        {
            _damageable.HealEvenly(entity.AsNullable(), amount, group);
            return;
        }

        var buckets = new List<(EntityUid Target, FixedPoint2 Damage)>();
        foreach (var organ in body.Organs.ContainedEntities)
        {
            if (!HasComp<WoundableComponent>(organ))
                continue;

            var organDamage = _wound.GetGroupDamage(organ, group);
            if (organDamage > FixedPoint2.Zero)
                buckets.Add((organ, organDamage));
        }

        var organless = FixedPoint2.Zero;
        foreach (var type in groupProto.DamageTypes)
            organless += _wound.GetOrganlessDamage(entity.Owner, type);

        if (organless > FixedPoint2.Zero)
            buckets.Add((entity.Owner, organless));

        var total = FixedPoint2.Zero;
        foreach (var (_, damage) in buckets)
            total += damage;

        if (total <= FixedPoint2.Zero)
        {
            // Nothing real anywhere we can see - fall back to the old plain behaviour rather
            // than silently doing nothing.
            _damageable.HealEvenly(entity.AsNullable(), amount, group);
            return;
        }

        var toHeal = -amount;
        var remaining = toHeal;
        for (var i = 0; i < buckets.Count; i++)
        {
            var (target, damage) = buckets[i];
            var share = FixedPoint2.Min(damage, i == buckets.Count - 1 ? remaining : FixedPoint2.Min(remaining, toHeal * damage / total));
            if (share <= FixedPoint2.Zero)
                continue;

            remaining -= HealBucket(entity, target, share, group);
        }

        // Rounding on the per-bucket shares above (or a bucket unable to absorb its full share)
        // can leave heal budget unspent - hand any leftover to buckets that still have damage
        // instead of dropping it.
        if (remaining > FixedPoint2.Zero)
        {
            foreach (var (target, damage) in buckets)
            {
                if (remaining <= FixedPoint2.Zero)
                    break;

                var share = FixedPoint2.Min(remaining, damage);
                if (share <= FixedPoint2.Zero)
                    continue;

                remaining -= HealBucket(entity, target, share, group);
            }
        }
    }

    /// <summary>
    /// Heals <paramref name="share"/> off <paramref name="target"/> and returns how much was
    /// actually removed (HealEvenly clamps to the target's current damage, which can be less
    /// than requested).
    /// </summary>
    private FixedPoint2 HealBucket(Entity<DamageableComponent> entity, EntityUid target, FixedPoint2 share, ProtoId<DamageGroupPrototype> group)
    {
        if (target != entity.Owner)
            return -_damageable.HealEvenly(target, -share, group).GetTotal();

        // The organless bucket lives directly on the mob - heal it in isolation so it doesn't
        // also fan back out to every organ via BodyDamageBridgeSystem's untargeted heal path,
        // which would double up on the per-organ shares above. Only add/remove the guard if we
        // weren't already inside one, so we don't strip a guard an outer caller still needs.
        var hadSkip = HasComp<SkipDamageBridgeComponent>(target);
        if (!hadSkip)
            AddComp<SkipDamageBridgeComponent>(target);

        var applied = -_damageable.HealEvenly(target, -share, group).GetTotal();

        if (!hadSkip)
            RemComp<SkipDamageBridgeComponent>(target);

        return applied;
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class EvenHealthChange : EntityEffectBase<EvenHealthChange>
{
    /// <summary>
    /// Damage to heal, collected into entire damage groups.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<ProtoId<DamageGroupPrototype>, FixedPoint2> Damage = new();

    /// <summary>
    /// Should this effect ignore damage modifiers?
    /// </summary>
    [DataField]
    public bool IgnoreResistances = true;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        var damages = new List<string>();
        var heals = false;
        var deals = false;

        var damagableSystem = entSys.GetEntitySystem<DamageableSystem>();
        var universalReagentDamageModifier = damagableSystem.UniversalReagentDamageModifier;
        var universalReagentHealModifier = damagableSystem.UniversalReagentHealModifier;

        foreach (var (group, amount) in Damage)
        {
            var groupProto = prototype.Index(group);

            var sign = FixedPoint2.Sign(amount);
            float mod;

            switch (sign)
            {
                case < 0:
                    heals = true;
                    mod = universalReagentHealModifier;
                    break;
                case > 0:
                    deals = true;
                    mod = universalReagentDamageModifier;
                    break;
                default:
                    continue; // Don't need to show damage types of 0...
            }

            damages.Add(
                Loc.GetString("health-change-display",
                    ("kind", groupProto.LocalizedName),
                    ("amount", MathF.Abs(amount.Float() * mod)),
                    ("deltasign", sign)
                ));
        }

        var healsordeals = heals ? deals ? "both" : "heals" : deals ? "deals" : "none";
        return Loc.GetString("entity-effect-guidebook-even-health-change",
            ("chance", Probability),
            ("changes", ContentLocalizationManager.FormatList(damages)),
            ("healsordeals", healsordeals));
    }
}
