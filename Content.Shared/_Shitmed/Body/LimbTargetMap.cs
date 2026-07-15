using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body;
using Robust.Shared.Prototypes;

namespace Content.Shared._Shitmed.Body;

public static class LimbTargetMap
{
    private static readonly Dictionary<TargetBodyPart, ProtoId<OrganCategoryPrototype>> TargetToCategory = new()
    {
        { TargetBodyPart.Head, "Head" },
        { TargetBodyPart.Chest, "Torso" },
        { TargetBodyPart.LeftArm, "ArmLeft" },
        { TargetBodyPart.RightArm, "ArmRight" },
        { TargetBodyPart.LeftLeg, "LegLeft" },
        { TargetBodyPart.RightLeg, "LegRight" },
    };

    /// <summary>
    /// Which limb-organ categories get cascade-removed when a given category is dismembered.
    /// </summary>
    private static readonly Dictionary<ProtoId<OrganCategoryPrototype>, ProtoId<OrganCategoryPrototype>[]> Hierarchy = new()
    {
        { "ArmLeft", new ProtoId<OrganCategoryPrototype>[] { "HandLeft" } },
        { "ArmRight", new ProtoId<OrganCategoryPrototype>[] { "HandRight" } },
        { "LegLeft", new ProtoId<OrganCategoryPrototype>[] { "FootLeft" } },
        { "LegRight", new ProtoId<OrganCategoryPrototype>[] { "FootRight" } },
        {
            "Torso", new ProtoId<OrganCategoryPrototype>[]
            {
                "ArmLeft", "ArmRight", "LegLeft", "LegRight", "Head",
            }
        },
    };

    /// <summary>
    /// Reverse of <see cref="TargetToCategory"/>, derived from it so the two can't drift.
    /// </summary>
    private static readonly Dictionary<ProtoId<OrganCategoryPrototype>, TargetBodyPart> CategoryToTarget =
        BuildReverseMap();

    private static Dictionary<ProtoId<OrganCategoryPrototype>, TargetBodyPart> BuildReverseMap()
    {
        var reverse = new Dictionary<ProtoId<OrganCategoryPrototype>, TargetBodyPart>();
        foreach (var (target, category) in TargetToCategory)
        {
            reverse.TryAdd(category, target);
        }

        return reverse;
    }

    public static bool TryGetCategory(TargetBodyPart target, out ProtoId<OrganCategoryPrototype> category)
    {
        return TargetToCategory.TryGetValue(target, out category);
    }

    public static bool TryGetTarget(ProtoId<OrganCategoryPrototype> category, out TargetBodyPart target)
    {
        return CategoryToTarget.TryGetValue(category, out target);
    }

    public static IReadOnlyList<ProtoId<OrganCategoryPrototype>> GetCascadeChildren(ProtoId<OrganCategoryPrototype> category)
    {
        return Hierarchy.TryGetValue(category, out var children) ? children : Array.Empty<ProtoId<OrganCategoryPrototype>>();
    }

    /// <summary>
    /// Reverse of <see cref="Hierarchy"/>: the category a given category cascades FROM,
    /// used as a damage-redirect fallback when a peripheral limb's integrity is exhausted
    /// (e.g. a hand redirects to its arm, an arm redirects to the torso).
    /// </summary>
    private static readonly Dictionary<ProtoId<OrganCategoryPrototype>, ProtoId<OrganCategoryPrototype>> ParentCategory =
        BuildParentMap();

    private static Dictionary<ProtoId<OrganCategoryPrototype>, ProtoId<OrganCategoryPrototype>> BuildParentMap()
    {
        var parents = new Dictionary<ProtoId<OrganCategoryPrototype>, ProtoId<OrganCategoryPrototype>>();
        foreach (var (parent, children) in Hierarchy)
        {
            foreach (var child in children)
            {
                parents[child] = parent;
            }
        }

        return parents;
    }

    public static bool TryGetParentCategory(ProtoId<OrganCategoryPrototype> category, out ProtoId<OrganCategoryPrototype> parent)
    {
        return ParentCategory.TryGetValue(category, out parent);
    }

    /// <summary>
    /// Which vital-organ categories (OrganIntegrityComponent-bearing organs: Brain/Eyes/etc)
    /// are anatomically housed within a given limb category. Peripheral limbs (arms/legs)
    /// intentionally have no entry - they house no vital organs.
    /// </summary>
    private static readonly Dictionary<ProtoId<OrganCategoryPrototype>, ProtoId<OrganCategoryPrototype>[]> VitalOrgansByLimb = new()
    {
        { "Head", new ProtoId<OrganCategoryPrototype>[] { "Brain", "Eyes", "Tongue", "Ears" } },
        { "Torso", new ProtoId<OrganCategoryPrototype>[] { "Heart", "Lungs", "Stomach", "Liver", "Kidneys", "Appendix" } },
    };

    public static IReadOnlyList<ProtoId<OrganCategoryPrototype>> GetVitalOrganCategories(ProtoId<OrganCategoryPrototype> limbCategory)
    {
        return VitalOrgansByLimb.TryGetValue(limbCategory, out var organs) ? organs : Array.Empty<ProtoId<OrganCategoryPrototype>>();
    }

    /// <summary>
    /// Finds the organ inside a body's Organs container carrying the given category.
    /// </summary>
    public static bool TryGetOrganByCategory(
        IEntityManager entityManager,
        BodyComponent body,
        ProtoId<OrganCategoryPrototype> category,
        out EntityUid organ)
    {
        organ = default;

        if (body.Organs is null)
            return false;

        foreach (var contained in body.Organs.ContainedEntities)
        {
            if (!entityManager.TryGetComponent(contained, out OrganComponent? organComp))
                continue;

            if (organComp.Category == category)
            {
                organ = contained;
                return true;
            }
        }

        return false;
    }
}
