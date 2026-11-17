using Content.Shared.Body;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Localizations;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Body;

/// <summary>
/// Heals organs of the body that metabolizes the reagent, a fixed amount of integrity per metabolism step.
/// Dead organs are past healing. The server system does the work.
/// </summary>
/// <inheritdoc cref="EntityEffect"/>
public sealed partial class HealOrgans : EntityEffectBase<HealOrgans>
{
    /// <summary>
    /// Organ categories to heal. Null heals every organ.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<OrganCategoryPrototype>>? Categories;

    /// <summary>
    /// Categories left alone even when <see cref="Categories"/> is null.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<OrganCategoryPrototype>> ExcludedCategories = [];

    /// <summary>
    /// Integrity restored to each organ per metabolism step.
    /// </summary>
    [DataField(required: true)]
    public FixedPoint2 Amount;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return Loc.GetString("entity-effect-guidebook-heal-organs",
            ("chance", Probability),
            ("amount", Amount.Float()),
            ("organs", OrganEffectText.Describe(Categories, ExcludedCategories)));
    }
}

/// <summary>
/// Damages organs of the body that metabolizes the reagent, a fixed amount of integrity per metabolism step.
/// The damage has no wound behind it, so only organ-healing reagents or a transplant fix it, not surgery.
/// </summary>
/// <inheritdoc cref="EntityEffect"/>
public sealed partial class DamageOrgans : EntityEffectBase<DamageOrgans>
{
    /// <summary>
    /// Organ categories to damage.
    /// </summary>
    [DataField(required: true)]
    public HashSet<ProtoId<OrganCategoryPrototype>> Categories = [];

    /// <summary>
    /// Integrity taken from each organ per metabolism step.
    /// </summary>
    [DataField(required: true)]
    public FixedPoint2 Amount;

    /// <summary>
    /// Chance rolled for each organ on its own, so a left and a right organ do not always wear down together.
    /// </summary>
    [DataField]
    public float OrganChance = 1f;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        var text = Loc.GetString("entity-effect-guidebook-damage-organs",
            ("chance", Probability),
            ("amount", Amount.Float()),
            ("organs", OrganEffectText.Describe(Categories, [])));

        // The reagent's own chance is added by the guidebook; the per-organ one has to be said here.
        return OrganChance < 1f
            ? text + Loc.GetString("entity-effect-guidebook-organ-chance", ("chance", OrganChance))
            : text;
    }
}

/// <summary>
/// Shared wording of the organ effects in the guidebook.
/// </summary>
public static class OrganEffectText
{
    public static string Describe(HashSet<ProtoId<OrganCategoryPrototype>>? categories, HashSet<ProtoId<OrganCategoryPrototype>> excluded)
    {
        if (categories != null)
            return ContentLocalizationManager.FormatList(Names(categories));

        return excluded.Count == 0
            ? Loc.GetString("entity-effect-guidebook-organs-all")
            : Loc.GetString("entity-effect-guidebook-organs-all-except", ("organs", ContentLocalizationManager.FormatList(Names(excluded))));
    }

    private static List<string> Names(IEnumerable<ProtoId<OrganCategoryPrototype>> categories)
    {
        var names = new List<string>();
        foreach (var category in categories)
        {
            names.Add(Loc.GetString($"entity-effect-guidebook-organ-{category.Id}"));
        }

        return names;
    }
}
