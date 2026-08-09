// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Polonium.Tutorial;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared._Polonium.Tutorial.Prototypes;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Tutorial;

/// <summary>
/// Main orchestrator — walks the player through a flow step by step.
/// Condition checks come from <see cref="TutorialConditionTracker"/>,
/// side-effects go through <see cref="TutorialActionExecutor"/>.
/// </summary>
public sealed class TutorialSystem : SharedTutorialSystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TutorialActionExecutor _actions = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TutorialStartRequestedEvent>(OnStartRequested);
        SubscribeLocalEvent<TutorialSessionComponent, ComponentShutdown>(OnShutdown);
    }

    /// <summary>Tracker calls this whenever something relevant happens - we check if the step is done.</summary>
    public void TryAdvance(EntityUid player, Predicate<Shared._Polonium.Tutorial.Conditions.TutorialCondition> predicate)
    {
        if (!TryComp<TutorialSessionComponent>(player, out var session))
            return;

        if (!TryGetCurrentStep(session, out _, out var stepProto))
            return;

        if (stepProto.Completion is null)
            return;

        if (!predicate(stepProto.Completion))
            return;

        AdvanceStep((player, session));
    }

    public void ForceAdvance(Entity<TutorialSessionComponent?> player)
    {
        if (!Resolve(player, ref player.Comp, false))
            return;

        AdvanceStep((player.Owner, player.Comp));
    }

    /// <summary>Dev/console entry point — same as <see cref="TutorialStartRequestedEvent"/>.</summary>
    public void ForceStartFlow(EntityUid player, ProtoId<TutorialFlowPrototype> flowId) =>
        StartFlow(player, flowId);

    private void OnStartRequested(TutorialStartRequestedEvent ev)
    {
        StartFlow(ev.Player, ev.Flow);
    }

    private void StartFlow(EntityUid player, ProtoId<TutorialFlowPrototype> flowId)
    {
        if (!_proto.TryIndex(flowId, out var flow))
        {
            Log.Error($"Tutorial: flow prototype '{flowId}' not found");
            return;
        }

        if (flow.Steps.Count == 0)
        {
            Log.Error($"Tutorial: flow '{flowId}' has no steps");
            return;
        }

        // already in a flow? nuke it and start over (respawn case)
        if (HasComp<TutorialSessionComponent>(player))
            RemComp<TutorialSessionComponent>(player);

        var session = AddComp<TutorialSessionComponent>(player);
        session.Flow = flowId;
        session.Anchors = ResolveAnchorsOnGrid(player);

        EnterStep((player, session), 0);
    }

    private void OnShutdown(Entity<TutorialSessionComponent> ent, ref ComponentShutdown args)
    {
    }

    private Dictionary<string, EntityUid> ResolveAnchorsOnGrid(EntityUid player)
    {
        var result = new Dictionary<string, EntityUid>();

        if (!TryComp(player, out TransformComponent? xform) || xform.GridUid is not { } grid)
        {
            Log.Warning($"Tutorial: player {ToPrettyString(player)} has no grid — anchors won't be resolved");
            return result;
        }

        var query = EntityQueryEnumerator<TutorialAnchorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var anchor, out var anchorXform))
        {
            if (anchorXform.GridUid != grid)
                continue;

            if (string.IsNullOrWhiteSpace(anchor.AnchorId))
                continue;

            // Duplicates are fine — multi-target conditions (AllAnchorsCleared) scan the grid themselves.
            // We just keep the first uid for single-target lookups (Reach, Reagent, etc.).
            result.TryAdd(anchor.AnchorId, uid);
        }

        Log.Debug($"Tutorial: resolved {result.Count} anchors on grid {grid} for {ToPrettyString(player)}");
        return result;
    }

    private void AdvanceStep(Entity<TutorialSessionComponent> ent)
    {
        if (TryGetCurrentStep(ent.Comp, out _, out var currentProto))
            _actions.ExecuteAll(ent.Owner, currentProto.OnComplete);

        var next = ent.Comp.CurrentStepIndex + 1;

        if (TryGetFlow(ent.Comp, out var flow) && next >= flow.Steps.Count)
        {
            CompleteFlow(ent);
            return;
        }

        EnterStep(ent, next);
    }

    private void EnterStep(Entity<TutorialSessionComponent> ent, int index)
    {
        if (!TryGetFlow(ent.Comp, out var flow))
            return;

        if (index < 0 || index >= flow.Steps.Count)
        {
            CompleteFlow(ent);
            return;
        }

        var stepId = flow.Steps[index];
        if (!_proto.TryIndex(stepId, out var stepProto))
        {
            Log.Error($"Tutorial: step prototype '{stepId}' not found, aborting flow");
            CompleteFlow(ent);
            return;
        }

        ent.Comp.CurrentStepIndex = index;
        ent.Comp.CurrentStep = stepId;
        ent.Comp.NavigationAnchor = stepProto.NavigationAnchor;
        ent.Comp.NavigationTarget = ResolveNavigationTarget(ent.Comp, stepProto);
        ent.Comp.StepStartedAt = _timing.CurTime;
        Dirty(ent);

        _actions.ExecuteAll(ent.Owner, stepProto.OnEnter);

        Log.Debug($"Tutorial: {ToPrettyString(ent.Owner)} entered step '{stepId}' ({index + 1}/{flow.Steps.Count})");
    }

    private void CompleteFlow(Entity<TutorialSessionComponent> ent)
    {
        Log.Debug($"Tutorial: {ToPrettyString(ent.Owner)} completed flow '{ent.Comp.Flow}'");

        ent.Comp.CurrentStep = null;
        ent.Comp.NavigationAnchor = null;
        ent.Comp.NavigationTarget = null;
        Dirty(ent);

        RemComp<TutorialSessionComponent>(ent.Owner);
    }

    private bool TryGetFlow(TutorialSessionComponent session, out TutorialFlowPrototype flow)
    {
        return _proto.TryIndex(session.Flow, out flow!);
    }

    private bool TryGetCurrentStep(
        TutorialSessionComponent session,
        out ProtoId<TutorialStepPrototype> stepId,
        out TutorialStepPrototype proto)
    {
        stepId = default;
        proto = default!;

        if (session.CurrentStep is not { } id)
            return false;

        if (!_proto.TryIndex(id, out var result))
            return false;

        stepId = id;
        proto = result;
        return true;
    }

    private NetEntity? ResolveNavigationTarget(TutorialSessionComponent session, TutorialStepPrototype step)
    {
        if (step.NavigationAnchor is not { } anchorId)
            return null;

        if (!session.Anchors.TryGetValue(anchorId, out var uid))
        {
            Log.Warning($"Tutorial: navigation anchor '{anchorId}' not found on grid");
            return null;
        }

        return GetNetEntity(uid);
    }
}
