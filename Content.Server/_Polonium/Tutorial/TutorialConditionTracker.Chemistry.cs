using System.Linq;
using System.Numerics;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared._Polonium.Tutorial.Conditions;
using Content.Shared._Polonium.Tutorial.Watchers;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Fluids.Components;
using Content.Shared.Interaction;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Tutorial;

/// <summary>
/// Reagents: what sits in a beaker or a tank, what is still on the floor as a puddle, and
/// what of that is close enough for the trainee to have done it.
/// </summary>
public sealed partial class TutorialConditionTracker
{
    /// <summary>
    /// Adds up a reagent across everything the trainee could put in a machine - on the floor
    /// nearby, in their hands, in their bags - counting only containers that will hand it over.
    /// </summary>
    private bool CheckDrainableReagent(EntityUid player, DrainableReagentNearbyCondition cond)
    {
        var origin = Transform(player).Coordinates;

        if (cond.AnchorId is { } anchorId)
        {
            var found = false;
            foreach (var anchor in AnchorsOnGrid(player, anchorId))
            {
                if (cond.Inside)
                    return SumDrainable(anchor, cond.Reagent) >= cond.Amount;

                origin = Transform(anchor).Coordinates;
                found = true;
                break;
            }

            if (!found)
                return false;
        }

        var seen = new HashSet<EntityUid>();

        foreach (var uid in _lookup.GetEntitiesInRange(origin, cond.Range))
            seen.Add(uid);

        foreach (var held in _hands.EnumerateHeld(player))
            seen.Add(held);

        var slots = _inventory.GetSlotEnumerator(player);
        while (slots.NextItem(out var item))
            seen.Add(item);

        var total = 0f;
        foreach (var uid in seen)
        {
            total += SumDrainable(uid, cond.Reagent);
            if (total >= cond.Amount)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Adds up a reagent across every container of one prototype the trainee could have left
    /// lying around. Narrow on purpose - a kitchen sink is full of water and would otherwise
    /// answer for the beaker.
    /// </summary>
    private bool CheckPrototypeSolution(EntityUid player, PrototypeSolutionContainsCondition cond)
    {
        var total = 0f;

        foreach (var uid in ReachableEntities(player, cond.Range))
        {
            if (MetaData(uid).EntityPrototype?.ID != cond.Prototype.Id)
                continue;

            if (cond.Solution is { } name)
            {
                if (_solution.TryGetSolution(uid, name, out _, out var single))
                    total += single.GetTotalPrototypeQuantity(cond.Reagent).Float();
            }
            else
            {
                foreach (var (_, soln) in _solution.EnumerateSolutions((uid, null)))
                    total += soln.Comp.Solution.GetTotalPrototypeQuantity(cond.Reagent).Float();
            }

            if (total >= cond.Amount)
                return true;
        }

        return false;
    }

    /// <summary>Everything around the trainee plus whatever they are carrying, bags included.</summary>
    private HashSet<EntityUid> ReachableEntities(EntityUid player, float range)
    {
        var seen = new HashSet<EntityUid>();

        foreach (var uid in _lookup.GetEntitiesInRange(Transform(player).Coordinates, range))
            Collect(uid, seen);

        foreach (var held in _hands.EnumerateHeld(player))
            Collect(held, seen);

        var slots = _inventory.GetSlotEnumerator(player);
        while (slots.NextItem(out var item))
            Collect(item, seen);

        return seen;
    }

    private void Collect(EntityUid uid, HashSet<EntityUid> seen)
    {
        if (!seen.Add(uid) || !TryComp<ContainerManagerComponent>(uid, out var containers))
            return;

        foreach (var container in _container.GetAllContainers(uid, containers))
        {
            foreach (var child in container.ContainedEntities)
                Collect(child, seen);
        }
    }

    /// <summary>
    /// What the anchor is holding itself. A hydroponics tray only turns soil reagents into water
    /// and nutrient levels once something is planted, so before planting this is the only way to
    /// tell that the trainee watered and fertilised it.
    /// </summary>
    private bool CheckAnchorSolution(EntityUid player, AnchorSolutionContainsCondition cond)
    {
        foreach (var uid in AnchorsOnGrid(player, cond.AnchorId))
        {
            var total = 0f;

            if (cond.Solution is { } name)
            {
                if (_solution.TryGetSolution(uid, name, out _, out var single))
                    total = single.GetTotalPrototypeQuantity(cond.Reagent).Float();
            }
            else
            {
                foreach (var (_, soln) in _solution.EnumerateSolutions((uid, null)))
                    total += soln.Comp.Solution.GetTotalPrototypeQuantity(cond.Reagent).Float();
            }

            if (total >= cond.Amount)
                return true;
        }

        return false;
    }

    private float SumDrainable(EntityUid uid, ProtoId<ReagentPrototype> reagent)
    {
        var total = 0f;

        if (_solution.TryGetDrainableSolution(uid, out _, out var solution))
            total += solution.GetTotalPrototypeQuantity(reagent).Float();

        if (!TryComp<ContainerManagerComponent>(uid, out var containers))
            return total;

        foreach (var container in _container.GetAllContainers(uid, containers))
        {
            foreach (var child in container.ContainedEntities)
                total += SumDrainable(child, reagent);
        }

        return total;
    }

    private bool CheckPuddleNearby(EntityUid player, TutorialSessionComponent session, PuddleNearbyWatcher watcher)
    {
        if (watcher.Reagent is { } reagent && session.Flags.Contains(IngestedReagentCondition.Flag(reagent)))
            return false;
        if (!TryComp(player, out TransformComponent? xform))
            return false;

        // anything at or below this is a dribble, not a spill
        var floor = MathF.Max(watcher.MinVolume, 0.01f);

        foreach (var uid in _lookup.GetEntitiesInRange(xform.Coordinates, watcher.Range))
        {
            if (!TryComp<PuddleComponent>(uid, out var puddle) || _container.IsEntityInContainer(uid))
                continue;

            if (!_solution.TryGetSolution(uid, puddle.SolutionName, out _, out var solution))
                continue;

            var volume = watcher.Reagent is { } wanted
                ? solution.GetTotalPrototypeQuantity(wanted)
                : solution.Volume;

            if (volume.Float() >= floor)
                return true;
        }

        return false;
    }

    private bool CheckPuddlesCleared(EntityUid player, PuddlesClearedCondition cond)
    {
        if (!TryComp(player, out TransformComponent? xform) || xform.GridUid is not { } grid)
            return false;

        // scoped version only looks around the anchor, a spill two rooms back is not this step's problem
        var origin = Vector2.Zero;
        var scoped = false;
        if (cond.NearAnchorId is { } anchorId)
        {
            foreach (var anchor in AnchorsOnGrid(player, anchorId))
            {
                origin = _transform.GetWorldPosition(anchor);
                scoped = true;
                break;
            }
        }

        var rangeSq = cond.Range * cond.Range;

        var query = EntityQueryEnumerator<PuddleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var puddle, out var px))
        {
            if (px.GridUid != grid)
                continue;

            if (_container.IsEntityInContainer(uid))
                continue;

            if (scoped && (_transform.GetWorldPosition(px) - origin).LengthSquared() > rangeSq)
                continue;

            // the mop leaves plain water behind and that dries up by itself. making the player
            // stand around watching it evaporate is not a lesson, so treat it as already clean
            if (IsSelfDrying(uid, puddle, cond.IgnoreReagents))
                continue;

            return false;
        }

        return true;
    }

    private bool IsSelfDrying(EntityUid uid, PuddleComponent puddle, List<ProtoId<ReagentPrototype>> ignored)
    {
        if (!_solution.ResolveSolution(uid, puddle.SolutionName, ref puddle.Solution, out var solution))
            return false;

        if (solution.Volume <= 0)
            return true;

        var evaporating = solution.GetTotalPrototypeQuantity(_puddle.GetEvaporatingReagents(solution));
        var skipped = ignored.Count > 0 ? solution.GetTotalPrototypeQuantity(ignored.ToArray()) : 0;
        return solution.Volume - evaporating - skipped <= 0;
    }
}
