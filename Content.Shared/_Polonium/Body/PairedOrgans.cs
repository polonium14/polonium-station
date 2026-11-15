using Content.Shared.Body;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Body;

/// <summary>
/// The player species have a left and a right lung and kidney, each its own organ category, because a body holds
/// one organ per category. Diona, slimes and animals keep a single organ of the old category, so code and
/// prototypes that mean "the lungs" or "the kidneys" keep naming the old category and match both sides through here.
/// </summary>
public static class PairedOrgans
{
    public static readonly ProtoId<OrganCategoryPrototype> Lungs = "Lungs";
    public static readonly ProtoId<OrganCategoryPrototype> Kidneys = "Kidneys";
    private static readonly ProtoId<OrganCategoryPrototype> LungLeft = "LungLeft";
    private static readonly ProtoId<OrganCategoryPrototype> LungRight = "LungRight";
    private static readonly ProtoId<OrganCategoryPrototype> KidneyLeft = "KidneyLeft";
    private static readonly ProtoId<OrganCategoryPrototype> KidneyRight = "KidneyRight";

    /// <summary>
    /// Whether an organ of this category counts as the wanted one: the same category, or one side of the wanted pair.
    /// </summary>
    public static bool Matches(ProtoId<OrganCategoryPrototype>? category, ProtoId<OrganCategoryPrototype> wanted)
    {
        return category == wanted
            || wanted == Lungs && (category == LungLeft || category == LungRight)
            || wanted == Kidneys && (category == KidneyLeft || category == KidneyRight);
    }

    /// <summary>
    /// How much of the whole an organ of this category is: half for one side of a pair, all of it otherwise.
    /// </summary>
    public static float Share(ProtoId<OrganCategoryPrototype>? category)
    {
        return category == LungLeft || category == LungRight || category == KidneyLeft || category == KidneyRight
            ? 0.5f
            : 1f;
    }
}
