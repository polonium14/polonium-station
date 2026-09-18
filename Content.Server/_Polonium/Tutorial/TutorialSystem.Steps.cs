using System.Linq;
using Content.Server.Database;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared._Polonium.Tutorial.Conditions;
using Content.Shared._Polonium.Tutorial.Prototypes;
using Content.Shared.CCVar;
using Content.Shared.Movement.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Tutorial;

/// <summary>
/// Moving between steps. Entering a step is the only place props are set up and flags are
/// wiped, so a step reached by advancing, by jumping or by an admin command is the same step.
/// </summary>
public sealed partial class TutorialSystem
{
    [Dependency] private TutorialConditionTracker _tracker = default!;

    public void ForceAdvance(Entity<TutorialSessionComponent?> player)
    {
        if (!Resolve(player, ref player.Comp, false))
            return;

        AdvanceStep((player.Owner, player.Comp));
    }

    private int ResolveDebugStartStep(TutorialFlowPrototype flow, out string? roomId)
    {
        roomId = null;
        var raw = _cfg.GetCVar(CCVars.TutorialDebugStartRoom);
        if (!TryNormalizeRoomId(raw, out var room))
        {
            if (!string.IsNullOrWhiteSpace(raw))
                Log.Warning($"Tutorial: debug_start_room '{raw}' is not a room marker, starting from the beginning");

            return 0;
        }

        // room0 is the hud briefing, no marker on the map
        if (room == "room0")
            return 0;

        var index = FindRoomStepIndex(flow, room);
        if (index < 0)
        {
            Log.Warning($"Tutorial: no step for '{room}', starting from the beginning");
            return 0;
        }

        roomId = room;
        Log.Info($"Tutorial: debug start at {room} (step '{flow.Steps[index]}', index {index})");
        return index;
    }

    private static bool TryNormalizeRoomId(string raw, out string roomId)
    {
        roomId = string.Empty;
        var s = raw.Trim();
        if (s.Length == 0)
            return false;

        if (s.StartsWith("room", StringComparison.OrdinalIgnoreCase))
            s = s[4..];
        else if (s.Length >= 2 && (s[0] is 'r' or 'R') && char.IsDigit(s[1]))
            s = s[1..];

        s = s.Trim();
        if (!int.TryParse(s, out var n) || n < 0)
            return false;

        roomId = $"room{n}";
        return true;
    }

    private int FindRoomStepIndex(TutorialFlowPrototype flow, string roomId)
    {
        for (var i = 0; i < flow.Steps.Count; i++)
        {
            if (_proto.TryIndex(flow.Steps[i], out var step) && step.SpeakAtAnchor == roomId)
                return i;
        }

        if (!int.TryParse(roomId.AsSpan("room".Length), out var n))
            return -1;

        var prefix = $"TutorialLinearR{n:D2}";
        for (var i = 0; i < flow.Steps.Count; i++)
        {
            if (flow.Steps[i].Id.StartsWith(prefix, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    private void FastForwardBefore(Entity<TutorialSessionComponent> ent, TutorialFlowPrototype flow, int startIndex)
    {
        for (var i = 0; i < startIndex; i++)
        {
            if (!_proto.TryIndex(flow.Steps[i], out var step))
                continue;

            // no speech, no meteor countdown, no walking npcs - just the world state this room needs
            _actions.ExecuteAll(ent.Owner, step.OnEnter, instant: true);
            _actions.ExecuteAll(ent.Owner, step.OnComplete, instant: true);
        }
    }

    // CurTime still moves while the map is paused, so shove every deadline forward by the gap
    public void ShiftIdleTimers(EntityUid trainee, TimeSpan delta)
    {
        if (delta <= TimeSpan.Zero)
            return;

        if (!TryComp<TutorialSessionComponent>(trainee, out var session))
            return;

        session.StepStartedAt += delta;
        if (session.PendingAdvanceAt is { } pending)
            session.PendingAdvanceAt = pending + delta;

        session.FlowStartedAt += delta;

        foreach (var key in session.HeldSince.Keys.ToList())
            session.HeldSince[key] += delta;

        if (TryComp<TutorialFrozenComponent>(trainee, out var frozen))
            frozen.ExpiresAt += delta;

        if (session.MentorUid is not { } mentor
            || Deleted(mentor)
            || !TryComp<TutorialMentorComponent>(mentor, out var mentorComp))
            return;

        mentorComp.NextSpeak += delta;
        mentorComp.QuipDoneAt += delta;
        foreach (var line in mentorComp.SpeechQueue)
            line.GateExpiresAt += delta;
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

            result.TryAdd(anchor.AnchorId, uid);
        }

        Log.Debug($"Tutorial: resolved {result.Count} anchors on grid {grid} for {ToPrettyString(player)}");
        return result;
    }

    /// <summary>
    /// Straight to a later step. The steps in between are skipped whole, their OnComplete included,
    /// so whatever they would have unlocked stays shut.
    /// </summary>
    public void JumpToStep(Entity<TutorialSessionComponent> ent, ProtoId<TutorialStepPrototype> stepId)
    {
        ent.Comp.JumpTo = null;

        if (!TryGetFlow(ent.Comp, out var flow))
            return;

        var index = flow.Steps.IndexOf(stepId);
        if (index < 0)
        {
            Log.Error($"Tutorial: jump target '{stepId}' is not in flow '{flow.ID}'");
            return;
        }

        EnterStep(ent, index);
    }

    private void AdvanceStep(Entity<TutorialSessionComponent> ent)
    {
        if (TryGetCurrentStep(ent.Comp, out _, out var currentProto))
            _actions.ExecuteAll(ent.Owner, currentProto.OnComplete);

        var next = ent.Comp.CurrentStepIndex + 1;

        if (TryGetFlow(ent.Comp, out var flow) && next >= flow.Steps.Count)
        {
            CompleteFlow(ent, redial: true);
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
        ent.Comp.HighlightAnchors = new List<string>(stepProto.HighlightAnchors);
        ent.Comp.KeybindHint = stepProto.KeybindHint;
        ent.Comp.Flags.Clear();
        ent.Comp.FiredWatchers.Clear();
        ent.Comp.HeldSince.Clear();
        ent.Comp.JumpTo = null;
        ent.Comp.CameraAtStepStart = TryComp<InputMoverComponent>(ent.Owner, out var mover)
            ? mover.TargetRelativeRotation
            : Angle.Zero;
        ent.Comp.FocusTarget = null;
        ent.Comp.TargetShots.Clear();
        ent.Comp.DrillShots = 0;
        ent.Comp.DrillHits = 0;
        ent.Comp.StepStartedAt = _timing.CurTime;
        ent.Comp.PendingAdvanceAt = null;
        ent.Comp.StuckHinted = false;
        Dirty(ent);

        // nothing to teach if they already did it, and the mentor must not ask for it anyway
        var skipIf = stepProto.SkipIf ?? (stepProto.SkipIfSatisfied ? stepProto.Completion : null);
        if (skipIf is { } skip && _tracker.Evaluate(ent.Owner, ent.Comp, skip))
        {
            AdvanceStep(ent);
            return;
        }

        _actions.ExecuteAll(ent.Owner, stepProto.OnEnter);
        // otherwise the whole row glows until the first poll picks a target
        _tracker.PrimeDrill(ent.Owner, ent.Comp, stepProto);
        _mentor.EnqueueStep(
            ent.Owner,
            ResolveSpeak(ent.Owner, stepProto.Speak),
            stepProto.SpeakAtAnchor,
            stepProto.SpeakAtRange,
            stepProto.SpeakHoldSeconds);

        if (stepProto.Finale)
            OnEnteredFinale(ent);

        Log.Debug($"Tutorial: {ToPrettyString(ent.Owner)} entered step '{stepId}' ({index + 1}/{flow.Steps.Count})");
    }

    private static bool TryGetRoomMarker(string stepId, out string room)
    {
        room = string.Empty;
        const string prefix = "TutorialLinearR";
        if (!stepId.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        var digits = 0;
        while (prefix.Length + digits < stepId.Length && char.IsDigit(stepId[prefix.Length + digits]))
            digits++;

        // room 0 is the hud briefing and has no marker
        if (!int.TryParse(stepId.AsSpan(prefix.Length, digits), out var n) || n <= 0)
            return false;

        room = $"room{n}";
        return true;
    }

    /// <summary>Standing on the anchor would count for the step, so do not put them there.</summary>
    private static bool Reaches(TutorialCondition? condition, string anchorId)
    {
        return condition switch
        {
            ReachAnchorCondition reach => reach.AnchorId == anchorId,
            AnyReachAnchorsCondition any => any.AnchorIds.Contains(anchorId),
            CrawlingReachCondition crawl => crawl.AnchorIds.Contains(anchorId),
            AnyCondition any => any.Conditions.Any(c => Reaches(c, anchorId)),
            AllCondition all => all.Conditions.Any(c => Reaches(c, anchorId)),
            NotCondition not => Reaches(not.Condition, anchorId),
            HeldCondition held => Reaches(held.Condition, anchorId),
            _ => false,
        };
    }
}
