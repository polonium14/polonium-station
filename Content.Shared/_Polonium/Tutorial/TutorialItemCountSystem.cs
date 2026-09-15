using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Lathe;
using Content.Shared.Research.Prototypes;
using Content.Shared.Stacks;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Tutorial;

/// <summary>
/// Counts what the trainee has gathered for a build: in their hands and bags, lying by a few places, or already
/// put into the frame standing on one of them. Shared so the lathe card and the step completion agree.
/// </summary>
public sealed partial class TutorialItemCountSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    public const float PlaceRange = 1.5f;

    public HashSet<EntityUid> Gather(EntityUid player, IEnumerable<EntityUid> places, float range = PlaceRange)
    {
        var found = new HashSet<EntityUid>();

        foreach (var held in _hands.EnumerateHeld(player))
            Collect(held, found);

        var slots = _inventory.GetSlotEnumerator(player);
        while (slots.NextItem(out var item))
            Collect(item, found);

        foreach (var place in places)
        {
            var placeXform = Transform(place);
            foreach (var near in _lookup.GetEntitiesInRange(placeXform.Coordinates, range))
            {
                if (near == player)
                    continue;

                if (_container.IsEntityInContainer(near))
                    continue;

                // loose things get opened up, and so does whatever stands right on the place: the frame and the
                // parts already in it. every other machine keeps its insides
                var xform = Transform(near);
                var onPlace = xform.ParentUid == placeXform.ParentUid
                              && (xform.LocalPosition - placeXform.LocalPosition).LengthSquared() < 0.25f;

                if (!xform.Anchored || onPlace && !HasComp<LatheComponent>(near))
                    Collect(near, found);
                else
                    found.Add(near);
            }
        }

        return found;
    }

    /// <summary>How much of what this recipe prints is among the gathered things. Stacks count by amount.</summary>
    public int CountRecipe(ProtoId<LatheRecipePrototype> recipeId, IEnumerable<EntityUid> gathered)
    {
        if (!_proto.TryIndex(recipeId, out var recipe)
            || recipe.Result is not { } result
            || !_proto.TryIndex(result, out var resultProto))
            return 0;

        var count = 0;
        if (resultProto.TryComp<StackComponent>(out var printed, EntityManager.ComponentFactory))
        {
            foreach (var uid in gathered)
            {
                if (TryComp<StackComponent>(uid, out var stack) && stack.StackTypeId == printed.StackTypeId)
                    count += stack.Count;
            }

            return count;
        }

        foreach (var uid in gathered)
        {
            if (MetaData(uid).EntityPrototype?.ID == resultProto.ID)
                count++;
        }

        return count;
    }

    private void Collect(EntityUid uid, HashSet<EntityUid> found)
    {
        if (!found.Add(uid) || !TryComp<ContainerManagerComponent>(uid, out var containers))
            return;

        foreach (var container in _container.GetAllContainers(uid, containers))
        {
            foreach (var child in container.ContainedEntities)
                Collect(child, found);
        }
    }
}
