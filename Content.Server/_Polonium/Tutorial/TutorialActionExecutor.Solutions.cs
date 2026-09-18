using System.Linq;
using Content.Server.Wires;
using Content.Shared._Polonium.Tutorial.Actions;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Tutorial;

/// <summary>
/// Emptying reagents back out of the room. A chemistry step ends with beakers in a state the
/// next trainee cannot tell apart from a fresh one, wherever the last one left them.
/// </summary>
public sealed partial class TutorialActionExecutor
{
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedContainerSystem _containers = default!;

    /// <summary>Tips the excess back out of something the trainee overfilled.</summary>
    private void DrainSolution(EntityUid player, DrainAnchorSolutionAction drain)
    {
        var keep = FixedPoint2.New(MathF.Max(drain.Keep, 0f));

        foreach (var uid in AnchorsNamed(player, drain.AnchorId))
        {
            foreach (var soln in SolutionsOf(uid, drain.Solution))
            {
                // RemoveReagent swaps entries around, so work off a snapshot
                foreach (var entry in soln.Comp.Solution.Contents.ToArray())
                {
                    if (entry.Quantity > keep)
                        _solution.RemoveReagent(soln, entry.Reagent, entry.Quantity - keep);
                }
            }
        }
    }

    /// <summary>Same rescue for something that came out of a vending machine and has no anchor.</summary>
    private void DrainPrototypeSolution(EntityUid player, DrainPrototypeSolutionAction drain)
    {
        var keep = FixedPoint2.New(MathF.Max(drain.Keep, 0f));

        foreach (var uid in Reachable(player, drain.Range))
        {
            if (MetaData(uid).EntityPrototype?.ID != drain.Prototype.Id)
                continue;

            foreach (var soln in SolutionsOf(uid, drain.Solution))
            {
                foreach (var entry in soln.Comp.Solution.Contents.ToArray())
                {
                    if (drain.Reagent is { } only && entry.Reagent.Prototype != only.Id)
                        continue;

                    if (entry.Quantity > keep)
                        _solution.RemoveReagent(soln, entry.Reagent, entry.Quantity - keep);
                }
            }
        }
    }

    private HashSet<EntityUid> Reachable(EntityUid player, float range)
    {
        var seen = new HashSet<EntityUid>();

        foreach (var uid in _lookup.GetEntitiesInRange(Transform(player).Coordinates, range))
            CollectInto(uid, seen);

        foreach (var held in _hands.EnumerateHeld(player))
            CollectInto(held, seen);

        var slots = _inventory.GetSlotEnumerator(player);
        while (slots.NextItem(out var item))
            CollectInto(item, seen);

        return seen;
    }

    private void CollectInto(EntityUid uid, HashSet<EntityUid> seen)
    {
        if (!seen.Add(uid) || !TryComp<ContainerManagerComponent>(uid, out var containers))
            return;

        foreach (var container in _containers.GetAllContainers(uid, containers))
        {
            foreach (var child in container.ContainedEntities)
                CollectInto(child, seen);
        }
    }

    private IEnumerable<Entity<SolutionComponent>> SolutionsOf(EntityUid uid, string? name)
    {
        if (name is { } wanted)
        {
            if (_solution.TryGetSolution(uid, wanted, out var single, out _) && single is { } found)
                yield return found;

            yield break;
        }

        foreach (var (_, soln) in _solution.EnumerateSolutions((uid, null)))
            yield return soln;
    }
}
