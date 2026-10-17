using System.Linq;
using System.Numerics;
using Content.Shared._Polonium.Tutorial;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared._Polonium.Tutorial.Conditions;
using Content.Shared.Chemistry.Components;
using Content.Shared.Electrocution;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Fluids;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Light.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Stacks;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Tutorial;

/// <summary>
/// What the trainee is holding, wearing or dragging around, and how much of it. Anything a
/// step asks about the contents of hands, pockets, bags or a stack is answered here.
/// </summary>
public sealed partial class TutorialConditionTracker
{
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private TutorialItemCountSystem _itemCount = default!;

    private bool CheckBothHands(EntityUid player, List<string> anchorIds)
    {
        var found = new HashSet<string>();
        foreach (var held in _hands.EnumerateHeld(player))
        {
            if (TryComp<TutorialAnchorComponent>(held, out var anchor) && anchorIds.Contains(anchor.AnchorId))
                found.Add(anchor.AnchorId);
        }

        return anchorIds.Count > 0 && anchorIds.All(found.Contains);
    }

    private bool CheckSlotContainsRecursive(EntityUid player, SlotContainsAnchorRecursiveCondition cond)
    {
        if (cond.AnchorIds.Count == 0)
            return false;

        if (!_inventory.TryGetSlotEntity(player, cond.Slot, out var slotEntity))
        {
            return !cond.Strict && InventoryHasAnchors(player, cond.AnchorIds);
        }

        var found = new HashSet<string>();
        CollectAnchorsRecursive(slotEntity.Value, found, cond.AnchorIds);
        if (cond.AnchorIds.All(found.Contains))
            return true;

        return !cond.Strict && InventoryHasAnchors(player, cond.AnchorIds);
    }

    private bool CheckToolQualities(EntityUid player, ToolQualitiesCondition cond)
    {
        if (cond.Qualities.Count == 0)
            return false;

        var carried = new List<EntityUid>();
        if (cond.Slot is { } slot)
        {
            if (!_inventory.TryGetSlotEntity(player, slot, out var worn))
                return false;

            CollectEntitiesRecursive(worn.Value, carried);
        }
        else
        {
            EntityUid? skipped = null;
            if (cond.OutsideSlot is { } outside && _inventory.TryGetSlotEntity(player, outside, out var skip))
                skipped = skip;

            var slots = _inventory.GetSlotEnumerator(player);
            while (slots.NextItem(out var item))
            {
                if (item == skipped)
                    continue;

                carried.Add(item);
                CollectEntitiesRecursive(item, carried);
            }

            foreach (var held in _hands.EnumerateHeld(player))
            {
                if (held == skipped)
                    continue;

                carried.Add(held);
                CollectEntitiesRecursive(held, carried);
            }
        }

        bool Carries(ProtoId<ToolQualityPrototype> quality) => carried.Any(uid => _tool.HasQuality(uid, quality.Id));

        return cond.RequireAll
            ? cond.Qualities.All(Carries)
            : cond.Qualities.Any(Carries);
    }

    private void CollectEntitiesRecursive(EntityUid uid, List<EntityUid> found)
    {
        if (!TryComp<ContainerManagerComponent>(uid, out var containers))
            return;

        foreach (var container in _container.GetAllContainers(uid, containers))
        {
            foreach (var child in container.ContainedEntities)
            {
                found.Add(child);
                CollectEntitiesRecursive(child, found);
            }
        }
    }

    private bool InventoryHasAnchors(EntityUid player, List<string> wanted)
    {
        var found = new HashSet<string>();
        var slots = _inventory.GetSlotEnumerator(player);
        while (slots.NextItem(out var item))
            CollectAnchorsRecursive(item, found, wanted);

        foreach (var held in _hands.EnumerateHeld(player))
            CollectAnchorsRecursive(held, found, wanted);

        return wanted.Count > 0 && wanted.All(found.Contains);
    }

    private void CollectAnchorsRecursive(EntityUid uid, HashSet<string> found, IReadOnlyCollection<string> wanted)
    {
        if (TryComp<TutorialAnchorComponent>(uid, out var anchor) && wanted.Contains(anchor.AnchorId))
            found.Add(anchor.AnchorId);

        if (found.Count == wanted.Count)
            return;

        // a pen or a plushie holds nothing, and enumerating its containers throws
        if (!TryComp<ContainerManagerComponent>(uid, out var containers))
            return;

        foreach (var container in _container.GetAllContainers(uid, containers))
        {
            foreach (var child in container.ContainedEntities)
            {
                CollectAnchorsRecursive(child, found, wanted);
                if (found.Count == wanted.Count)
                    return;
            }
        }
    }

    private bool CheckPulling(EntityUid player, TutorialSessionComponent session, ItemPulledCondition cond)
    {
        if (!TryComp<PullerComponent>(player, out var puller) || puller.Pulling is not { } pulling)
            return false;

        return TryComp<TutorialAnchorComponent>(pulling, out var anchor) && anchor.AnchorId == cond.AnchorId
               || session.Anchors.TryGetValue(cond.AnchorId, out var target) && pulling == target;
    }

    private bool CheckAmmoEmpty(EntityUid player, AnchorAmmoEmptyCondition drained)
    {
        foreach (var uid in AnchorsOnGrid(player, drained.AnchorId))
        {
            var ev = new GetAmmoCountEvent();
            RaiseLocalEvent(uid, ref ev);
            var empty = ev.Capacity > 0 && ev.Count == 0;
            if (empty != drained.Every)
                return empty;
        }

        return drained.Every;
    }

    private int CountFullAmmo(EntityUid player, string anchorId)
    {
        var full = 0;
        foreach (var uid in AnchorsOnGrid(player, anchorId))
        {
            // the event instead of the component, the ballistic provider is locked to the gun system
            var ev = new GetAmmoCountEvent();
            RaiseLocalEvent(uid, ref ev);
            if (ev.Capacity > 0 && ev.Count >= ev.Capacity)
                full++;
        }

        return full;
    }

    private bool CheckToggled(EntityUid player, ItemToggledCondition toggle)
    {
        return AnyAnchor(player, toggle.AnchorId, uid =>
        {
            if (TryComp<ItemToggleComponent>(uid, out var item))
                return item.Activated == toggle.Activated;

            // lanterns and flashlights run their own toggle and never get an ItemToggle
            if (TryComp<HandheldLightComponent>(uid, out var light))
                return light.Activated == toggle.Activated;

            return false;
        });
    }

    private bool CheckHolding(EntityUid player, string anchorId, bool wielded = false)
    {
        foreach (var held in _hands.EnumerateHeld(player))
        {
            if (TryComp<TutorialAnchorComponent>(held, out var anchor)
                && anchor.AnchorId == anchorId
                && (!wielded || IsWielded(held)))
                return true;
        }

        return false;
    }

    /// <summary>
    /// A mop works by trading the water it holds for whatever is on the floor. Once the water is
    /// gone it silently stops working, which looks like a broken tutorial step.
    /// </summary>
    private bool CheckAbsorbentSpent(EntityUid player)
    {
        foreach (var held in _hands.EnumerateHeld(player))
        {
            if (!TryComp<AbsorbentComponent>(held, out var absorbent))
                continue;

            if (!_solution.TryGetSolution(held, absorbent.SolutionName, out _, out var solution))
                continue;

            if (solution.Volume <= 0)
                continue;

            if (solution.GetTotalPrototypeQuantity(_puddle.GetAbsorbentReagents(solution)) <= 0)
                return true;
        }

        return false;
    }

    private bool CheckHoldingPrototype(EntityUid player, string proto)
    {
        foreach (var held in _hands.EnumerateHeld(player))
        {
            if (MetaData(held).EntityPrototype?.ID == proto)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Counts matching entities lying around the anchor plus whatever the trainee is carrying,
    /// because half the time the ingredients are already in their hands or backpack.
    /// </summary>
    private int CountPrototype(EntityUid player, PrototypeNearAnchorCondition cond)
    {
        var seen = new HashSet<EntityUid>();
        var origin = Transform(player).Coordinates;

        if (cond.AnchorId is { } anchorId)
        {
            var found = false;
            foreach (var anchor in AnchorsOnGrid(player, anchorId))
            {
                // "inside the microwave" is a question about its contents, nothing else
                if (cond.Inside)
                    return CountPrototypeIn(anchor, cond.Prototype);

                origin = Transform(anchor).Coordinates;
                found = true;
                break;
            }

            if (!found)
                return 0;
        }

        foreach (var uid in _lookup.GetEntitiesInRange(origin, cond.Range))
            seen.Add(uid);

        if (!cond.IgnoreCarried)
        {
            foreach (var held in _hands.EnumerateHeld(player))
                seen.Add(held);

            var slots = _inventory.GetSlotEnumerator(player);
            while (slots.NextItem(out var item))
                seen.Add(item);
        }

        var count = 0;
        foreach (var uid in seen)
        {
            // a bolted down thing is never inside anything, so no need to look into containers
            if (cond.Anchored)
                count += Transform(uid).Anchored && MetaData(uid).EntityPrototype?.ID == cond.Prototype.Id ? 1 : 0;
            else
                count += CountPrototypeIn(uid, cond.Prototype);
        }

        return count;
    }

    private int CountPrototypeIn(EntityUid uid, string proto)
    {
        var count = MetaData(uid).EntityPrototype?.ID == proto ? 1 : 0;

        if (!TryComp<ContainerManagerComponent>(uid, out var containers))
            return count;

        foreach (var container in _container.GetAllContainers(uid, containers))
        {
            foreach (var child in container.ContainedEntities)
                count += CountPrototypeIn(child, proto);
        }

        return count;
    }

    private bool CheckPrototypesOnAnchors(EntityUid player, PrototypesOnAnchorsCondition cond)
    {
        var tiles = new HashSet<(EntityUid, Vector2i)>();
        var markers = new List<(EntityUid Uid, (EntityUid, Vector2i) Tile)>();

        foreach (var id in cond.AnchorIds)
        {
            var anchor = AnchorsOnGrid(player, id).FirstOrDefault();
            if (!anchor.IsValid() || TileOf(anchor) is not { } tile)
                return false;

            tiles.Add(tile);
            markers.Add((anchor, tile));
        }

        if (cond.Stray)
        {
            foreach (var (uid, _) in markers)
            {
                foreach (var near in _lookup.GetEntitiesInRange(Transform(uid).Coordinates, cond.StrayRange))
                {
                    if (Matches(near) && TileOf(near) is { } tile && !tiles.Contains(tile))
                        return true;
                }
            }

            return false;
        }

        foreach (var (uid, tile) in markers)
        {
            // a flatpack dropped at the cursor can land anywhere on the tile, so look a bit past the centre
            var filled = false;
            foreach (var near in _lookup.GetEntitiesInRange(Transform(uid).Coordinates, 0.75f))
            {
                if (Matches(near) && TileOf(near) == tile && RotationFits(near, uid))
                {
                    filled = true;
                    break;
                }
            }

            if (!filled)
                return false;
        }

        return true;

        bool Matches(EntityUid uid)
        {
            if (MetaData(uid).EntityPrototype is not { } proto
                || !cond.Prototypes.Any(p => p.Id == proto.ID)
                || _container.IsEntityInContainer(uid))
                return false;

            return !cond.Anchored || Transform(uid).Anchored;
        }

        bool RotationFits(EntityUid uid, EntityUid marker)
        {
            if (cond.Rotation == TutorialRotationCheck.Any)
                return true;

            var same = Transform(uid).LocalRotation.GetCardinalDir() == Transform(marker).LocalRotation.GetCardinalDir();
            return same == (cond.Rotation == TutorialRotationCheck.Match);
        }
    }

    private bool CheckLatheGuideGathered(EntityUid player, TutorialSessionComponent session)
    {
        if (!_tutorial.TryGetCurrentStep(session, out _, out var step) || step.LatheGuide is not { } guide)
            return false;

        var places = new List<EntityUid>();
        places.AddRange(AnchorsOnGrid(player, guide.Lathe));
        foreach (var id in guide.CountAt)
            places.AddRange(AnchorsOnGrid(player, id));

        var gathered = _itemCount.Gather(player, places);
        return guide.Items.All(item => _itemCount.CountRecipe(item.Recipe, gathered) >= item.Count);
    }

    private int CountStackNear(EntityUid player, StackAmountNearCondition cond)
    {
        var places = cond.AnchorId is { } anchorId
            ? AnchorsOnGrid(player, anchorId).ToList()
            : new List<EntityUid> { player };

        var total = 0;
        foreach (var uid in _itemCount.Gather(player, places, cond.Range))
        {
            if (TryComp<StackComponent>(uid, out var stack) && stack.StackTypeId == cond.StackType)
                total += stack.Count;
        }

        return total;
    }

    // the same question the welder asks before it burns anyone's eyes
    private bool IsEyeProtected(EntityUid player)
    {
        var ev = new GetEyeProtectionEvent();
        RaiseLocalEvent(player, ev);
        return ev.Protection > TimeSpan.Zero;
    }

    private static readonly LocId GlovesOffLine = "tutorial-holopad-r16-gloves-off";

    private void ProcessGloves(EntityUid player)
    {
        if (!TryComp<TutorialSessionComponent>(player, out var session) || !session.RequireInsulatedGloves)
            return;

        if (WearsInsulatedGloves(player))
        {
            session.GlovesWarned = false;
            return;
        }

        if (session.GlovesWarned)
            return;

        session.GlovesWarned = true;
        _mentor.Enqueue(player, new[] { GlovesOffLine });
    }

    public bool WearsInsulatedGloves(EntityUid player)
    {
        return _inventory.TryGetSlotEntity(player, "gloves", out var gloves)
               && HasComp<InsulatedComponent>(gloves);
    }

    private bool CheckHeld(EntityUid player, TutorialSessionComponent session, HeldCondition held)
    {
        if (!Evaluate(player, session, held.Condition))
        {
            session.HeldSince.Remove(held);
            return false;
        }

        if (!session.HeldSince.TryGetValue(held, out var since))
        {
            since = _timing.CurTime;
            session.HeldSince[held] = since;
        }

        return _timing.CurTime - since >= TimeSpan.FromSeconds(held.Seconds);
    }
}
