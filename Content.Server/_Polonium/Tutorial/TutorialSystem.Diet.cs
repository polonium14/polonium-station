using Content.Shared.Body.Components;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Tutorial;

public sealed partial class TutorialSystem
{
    private static readonly ProtoId<TagPrototype> MeatTag = "Meat";
    private static readonly LocId BurgerEatSpeak = "tutorial-holopad-r13-eat";
    private static readonly LocId BurgerEatSpeak2 = "tutorial-holopad-r13-eat-2";
    private static readonly LocId BurgerEatMaybe = "tutorial-holopad-r13-eat-maybe";
    private static readonly LocId BurgerEatMaybe2 = "tutorial-holopad-r13-eat-maybe-2";
    private static readonly LocId BurgerEatCannot = "tutorial-holopad-r13-eat-cannot";
    private static readonly LocId BurgerEatCannot2 = "tutorial-holopad-r13-eat-cannot-2";

    private IReadOnlyList<LocId> ResolveSpeak(EntityUid player, List<LocId> lines)
    {
        var needsDiet = false;
        foreach (var line in lines)
        {
            if (line == BurgerEatSpeak || line == BurgerEatSpeak2)
            {
                needsDiet = true;
                break;
            }
        }

        if (!needsDiet)
            return lines;

        var diet = BurgerEatLoc(player);
        var resolved = new List<LocId>(lines.Count);
        foreach (var line in lines)
        {
            if (line == BurgerEatSpeak)
                resolved.Add(diet);
            else if (line == BurgerEatSpeak2)
                resolved.Add(DietSecond(diet));
            else
                resolved.Add(line);
        }

        return resolved;
    }

    private static LocId DietSecond(LocId first)
    {
        if (first == BurgerEatMaybe)
            return BurgerEatMaybe2;
        if (first == BurgerEatCannot)
            return BurgerEatCannot2;
        return BurgerEatSpeak2;
    }

    private LocId BurgerEatLoc(EntityUid player)
    {
        if (!_body.TryGetOrgansWithComponent<StomachComponent>(player, out var stomachs))
            return BurgerEatCannot;

        var canEat = false;
        var exclusiveDiet = false;
        foreach (var stomach in stomachs)
        {
            var special = stomach.Comp.SpecialDigestible;
            if (special == null || !stomach.Comp.IsSpecialDigestibleExclusive)
            {
                canEat = true;
                continue;
            }

            exclusiveDiet = true;
            if (special.Tags != null && special.Tags.Contains(MeatTag))
                canEat = true;
        }

        if (!canEat)
            return BurgerEatCannot;

        if (exclusiveDiet)
            return BurgerEatMaybe;

        return BurgerEatSpeak;
    }
}
