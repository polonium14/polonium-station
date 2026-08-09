// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Shared._Polonium.Tutorial;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared._Polonium.Tutorial.Conditions;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Hands;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Movement.Pulling.Components;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Tutorial;

/// <summary>
/// Watches game events and polls for completion of the active step's condition.
/// One method per condition kind — easier to read than a giant switch.
/// </summary>
public sealed class TutorialConditionTracker : EntitySystem
{
    [Dependency] private readonly TutorialSystem _tutorial = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;

    private static readonly TimeSpan PollingInterval = TimeSpan.FromMilliseconds(250);

    private TimeSpan _nextPoll;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TutorialAnchorComponent, GotEquippedHandEvent>(OnPickedUp);
        SubscribeLocalEvent<TutorialAnchorComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<TutorialAnchorComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<TutorialAnchorComponent, ComponentShutdown>(OnAnchorRemoved);

        SubscribeNetworkEvent<TutorialAcknowledgeStepEvent>(OnAcknowledge);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextPoll)
            return;

        _nextPoll = _timing.CurTime + PollingInterval;
        PollSessions();
    }

    #region Event handlers

    private void OnPickedUp(Entity<TutorialAnchorComponent> anchor, ref GotEquippedHandEvent ev)
    {
        _tutorial.TryAdvance(ev.User, condition =>
            condition is PickUpAnchorCondition pickup && pickup.AnchorId == anchor.Comp.AnchorId);
    }

    private void OnInteractHand(Entity<TutorialAnchorComponent> anchor, ref InteractHandEvent ev)
    {
        _tutorial.TryAdvance(ev.User, condition =>
            condition is InteractAnchorCondition interact && interact.AnchorId == anchor.Comp.AnchorId);
    }

    private void OnUseInHand(Entity<TutorialAnchorComponent> anchor, ref UseInHandEvent ev)
    {
        _tutorial.TryAdvance(ev.User, condition =>
            condition is InteractAnchorCondition interact && interact.AnchorId == anchor.Comp.AnchorId);
    }

    private void OnAcknowledge(TutorialAcknowledgeStepEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } player)
            return;

        if (!TryComp<TutorialSessionComponent>(player, out var session))
            return;

        // Stale click from a previous step? Drop it — otherwise a slow click could skip the next step.
        if (session.CurrentStep is not { } current || current.Id != ev.StepId)
            return;

        _tutorial.TryAdvance(player, c => c is ManualAcknowledgeCondition);
    }

    // Catches "trashbag thrown into disposals" — disposals just delete the entity.
    private void OnAnchorRemoved(Entity<TutorialAnchorComponent> anchor, ref ComponentShutdown args)
    {
        var anchorId = anchor.Comp.AnchorId;
        if (string.IsNullOrWhiteSpace(anchorId))
            return;

        // Notify every active session — we don't know which player made it disappear.
        var query = EntityQueryEnumerator<TutorialSessionComponent>();
        while (query.MoveNext(out var playerUid, out _))
        {
            _tutorial.TryAdvance(playerUid, c =>
                c is EntityDeletedCondition del && del.AnchorId == anchorId);
        }
    }

    #endregion

    #region Polling

    private void PollSessions()
    {
        var query = EntityQueryEnumerator<TutorialSessionComponent>();
        while (query.MoveNext(out var playerUid, out var session))
        {
            _tutorial.TryAdvance(playerUid, condition => EvaluatePolled(playerUid, session, condition));
        }
    }

    private bool EvaluatePolled(EntityUid player, TutorialSessionComponent session, TutorialCondition condition)
    {
        return condition switch
        {
            ReachAnchorCondition reach                 => CheckReach(player, session, reach),
            AllAnchorsClearedCondition cleared         => CheckAllCleared(player, cleared),
            SlotContainsAnchorRecursiveCondition slot  => CheckSlotContainsRecursive(player, slot),
            ItemReagentContainsCondition reagent       => CheckReagentInAnchor(session, reagent),
            ItemPulledCondition pull                   => CheckPulling(player, session, pull),
            _ => false,
        };
    }

    private bool CheckReach(EntityUid player, TutorialSessionComponent session, ReachAnchorCondition reach)
    {
        // Scan all anchors with this id — handles "approach any of N" cases (multiple disposals etc.).
        if (!TryComp(player, out TransformComponent? playerXform) || playerXform.GridUid is not { } grid)
            return false;

        var playerPos = _transform.GetWorldPosition(playerXform);
        var rangeSq = reach.Range * reach.Range;

        var query = EntityQueryEnumerator<TutorialAnchorComponent, TransformComponent>();
        while (query.MoveNext(out _, out var anchor, out var anchorXform))
        {
            if (anchor.AnchorId != reach.AnchorId || anchorXform.GridUid != grid)
                continue;

            var distSq = (_transform.GetWorldPosition(anchorXform) - playerPos).LengthSquared();
            if (distSq <= rangeSq)
                return true;
        }

        return false;
    }

    private bool CheckAllCleared(EntityUid player, AllAnchorsClearedCondition cond)
    {
        // "Cleared" = either deleted, or sitting inside any container (trashbag, locker, disposals…).
        // Without the container check, picked-up trash still counts as on the floor.
        if (!TryComp(player, out TransformComponent? xform) || xform.GridUid is not { } grid)
            return false;

        var query = EntityQueryEnumerator<TutorialAnchorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var anchor, out var anchorXform))
        {
            if (anchor.AnchorId != cond.AnchorId)
                continue;

            if (anchorXform.GridUid != grid)
                continue;

            if (_container.IsEntityInContainer(uid))
                continue;

            return false;
        }

        return true;
    }

    private bool CheckSlotContainsRecursive(EntityUid player, SlotContainsAnchorRecursiveCondition cond)
    {
        if (cond.AnchorIds.Count == 0)
            return false;

        if (!_inventory.TryGetSlotEntity(player, cond.Slot, out var slotEntity))
            return false;

        var found = new HashSet<string>();
        CollectAnchorsRecursive(slotEntity.Value, found, cond.AnchorIds);

        return cond.AnchorIds.All(found.Contains);
    }

    private void CollectAnchorsRecursive(EntityUid uid, HashSet<string> found, IReadOnlyCollection<string> wanted)
    {
        if (TryComp<TutorialAnchorComponent>(uid, out var anchor)
            && wanted.Contains(anchor.AnchorId))
        {
            found.Add(anchor.AnchorId);
        }

        // Bail early if we've matched everything.
        if (found.Count == wanted.Count)
            return;

        foreach (var container in _container.GetAllContainers(uid))
        {
            foreach (var child in container.ContainedEntities)
            {
                CollectAnchorsRecursive(child, found, wanted);
                if (found.Count == wanted.Count)
                    return;
            }
        }
    }

    private bool CheckReagentInAnchor(TutorialSessionComponent session, ItemReagentContainsCondition cond)
    {
        if (!session.Anchors.TryGetValue(cond.AnchorId, out var uid))
            return false;

        // Total up the reagent across every solution on the entity.
        var total = 0f;
        foreach (var (_, solutionEnt) in _solution.EnumerateSolutions((uid, null)))
        {
            foreach (var reagent in solutionEnt.Comp.Solution.Contents)
            {
                if (reagent.Reagent.Prototype == cond.Reagent)
                    total += reagent.Quantity.Float();
            }
        }

        return total >= cond.MinUnits;
    }

    private bool CheckPulling(EntityUid player, TutorialSessionComponent session, ItemPulledCondition cond)
    {
        if (!session.Anchors.TryGetValue(cond.AnchorId, out var target))
            return false;

        return TryComp<PullerComponent>(player, out var puller) && puller.Pulling == target;
    }

    #endregion
}
