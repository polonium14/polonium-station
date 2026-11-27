using System.Linq;
using Content.Shared._Polonium.Body;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared.Body;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Polonium.Body;

/// <summary>
/// Heals and damages organ integrity directly, for reagents and the body debug window.
/// </summary>
public sealed partial class OrganHealingSystem : EntitySystem
{
    [Dependency] private TraumaSystem _trauma = default!;

    /// <summary>
    /// Modifier name of organ damage dealt without a wound, e.g. by an overdose.
    /// </summary>
    public const string ChemicalDamageId = "ChemicalDamage";

    /// <summary>
    /// Modifier name of the integrity a failure took for good. Nothing heals it; it goes with the organ.
    /// </summary>
    public const string ScarId = "FailureScar";

    /// <summary>
    /// Takes away organ damage. Trauma damage goes first, the way surgery heals it, then the rest.
    /// </summary>
    public void HealOrgan(Entity<OrganIntegrityComponent> organ, FixedPoint2 amount)
    {
        if (amount <= FixedPoint2.Zero || organ.Comp.IntegrityModifiers.Count == 0)
            return;

        var modifiers = organ.Comp.IntegrityModifiers
            .Where(modifier => modifier.Value > FixedPoint2.Zero && modifier.Key.Item1 != ScarId)
            .OrderByDescending(modifier => HasComp<TraumaComponent>(modifier.Key.Item2))
            .ToArray();

        // An organ with a floor (the brain) keeps counting damage below it, where nobody can see it.
        // That damage heals for free, so the first dose already shows on the analyzer.
        var hidden = modifiers.Aggregate(FixedPoint2.Zero, (sum, modifier) => sum + modifier.Value)
                     - (organ.Comp.IntegrityCap - organ.Comp.IntegrityFloor);
        if (hidden > FixedPoint2.Zero)
            amount += hidden;

        foreach (var ((id, owner), value) in modifiers)
        {
            if (amount <= FixedPoint2.Zero)
                return;

            if (value <= amount)
            {
                amount -= value;
                _trauma.TryRemoveOrganDamageModifier(organ, owner, id, organ.Comp);
                continue;
            }

            _trauma.TryChangeOrganDamageModifier(organ, -amount, owner, id, organ.Comp);
            return;
        }
    }

    /// <summary>
    /// Whether a failure has left its lasting mark on the organ.
    /// </summary>
    public bool IsScarred(Entity<OrganIntegrityComponent> organ)
    {
        return organ.Comp.IntegrityModifiers.ContainsKey((ScarId, organ.Owner));
    }

    /// <summary>
    /// Integrity a failure took from the organ for good; zero without a scar.
    /// </summary>
    public FixedPoint2 GetScar(Entity<OrganIntegrityComponent> organ)
    {
        return organ.Comp.IntegrityModifiers.GetValueOrDefault((ScarId, organ.Owner));
    }

    /// <summary>
    /// Takes a fraction of the organ's maximum integrity away for good, once per organ.
    /// The scar comes out of the damage the organ already has, so scarring a sick organ does not kill it:
    /// the organ only loses integrity if it was healthier than the scar allows.
    /// </summary>
    public void Scar(Entity<OrganIntegrityComponent> organ, float fraction)
    {
        if (fraction <= 0f || IsScarred(organ))
            return;

        var amount = organ.Comp.IntegrityCap * fraction;
        // Healed first: adding the scar on top of the damage could take the organ to zero and destroy it.
        HealOrgan(organ, amount);
        _trauma.TryCreateOrganDamageModifier(organ, amount, organ, ScarId, organ.Comp);
    }

    /// <summary>
    /// Damages an organ without a wound. Only healing reagents or a transplant fix this damage, surgery does not see it.
    /// </summary>
    public void DamageOrgan(Entity<OrganIntegrityComponent> organ, FixedPoint2 amount)
    {
        if (amount <= FixedPoint2.Zero)
            return;

        // Owned by the organ itself, so the damage moves with it when it is transplanted.
        if (!_trauma.TryChangeOrganDamageModifier(organ, amount, organ, ChemicalDamageId, organ.Comp))
            _trauma.TryCreateOrganDamageModifier(organ, amount, organ, ChemicalDamageId, organ.Comp);
    }

    /// <summary>
    /// Organs of a body in the given categories that still have integrity. Copied, because damage can kill an organ
    /// and swap it for a dead one while the caller is still going through the list.
    /// </summary>
    public List<Entity<OrganIntegrityComponent>> GetLivingOrgans(EntityUid body,
        HashSet<ProtoId<OrganCategoryPrototype>>? categories,
        HashSet<ProtoId<OrganCategoryPrototype>> excluded)
    {
        var organs = new List<Entity<OrganIntegrityComponent>>();
        if (!TryComp<BodyComponent>(body, out var bodyComp) || bodyComp.Organs is not { } container)
            return organs;

        foreach (var organ in container.ContainedEntities)
        {
            if (!TryComp<OrganIntegrityComponent>(organ, out var integrity)
                || CompOrNull<OrganComponent>(organ)?.Category is not { } category
                || categories != null && !MatchesAny(category, categories)
                || MatchesAny(category, excluded))
                continue;

            organs.Add((organ, integrity));
        }

        return organs;
    }

    private static bool MatchesAny(ProtoId<OrganCategoryPrototype> category, HashSet<ProtoId<OrganCategoryPrototype>> wanted)
    {
        foreach (var other in wanted)
        {
            if (PairedOrgans.Matches(category, other))
                return true;
        }

        return false;
    }
}

/// <inheritdoc cref="HealOrgans"/>
public sealed partial class HealOrgansEntityEffectSystem : EntityEffectSystem<BodyComponent, HealOrgans>
{
    [Dependency] private OrganHealingSystem _healing = default!;

    protected override void Effect(Entity<BodyComponent> entity, ref EntityEffectEvent<HealOrgans> args)
    {
        var amount = args.Effect.Amount * args.Scale;
        foreach (var organ in _healing.GetLivingOrgans(entity, args.Effect.Categories, args.Effect.ExcludedCategories))
        {
            _healing.HealOrgan(organ, amount);
        }
    }
}

/// <inheritdoc cref="DamageOrgans"/>
public sealed partial class DamageOrgansEntityEffectSystem : EntityEffectSystem<BodyComponent, DamageOrgans>
{
    [Dependency] private OrganHealingSystem _healing = default!;
    [Dependency] private IRobustRandom _random = default!;

    protected override void Effect(Entity<BodyComponent> entity, ref EntityEffectEvent<DamageOrgans> args)
    {
        var amount = args.Effect.Amount * args.Scale;
        foreach (var organ in _healing.GetLivingOrgans(entity, args.Effect.Categories, []))
        {
            if (args.Effect.OrganChance < 1f && !_random.Prob(args.Effect.OrganChance))
                continue;

            _healing.DamageOrgan(organ, amount);
        }
    }
}
