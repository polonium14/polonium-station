using System.Linq;
using Content.Server.Chat.Systems;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared.Chat;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Tutorial;

public sealed partial class TutorialMentorSystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPointLightSystem _lights = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _players = default!;

    private static readonly EntProtoId MentorProto = "TutorialHoloMentor";

    /// <summary>Set on the session once a line of the current step has been said out loud.</summary>
    public const string SpokeFlag = "spoke";

    // let the player look around after spawning before anyone starts talking at them
    private static readonly TimeSpan FirstSpeakDelay = TimeSpan.FromSeconds(4);

    // gap scales with how much there is to read, clamped so short lines never machine gun
    // and long ones never drag. roughly 30 characters per second plus a beat to notice the line
    private const double GapBaseSeconds = 0.8;
    private const double GapSecondsPerChar = 1.0 / 30.0;
    private const double GapMinSeconds = 2.0;
    private const double GapMaxSeconds = 4.5;

    // breather after a step flips, gives the player a moment before the next room gets explained
    private static readonly TimeSpan StepSettle = TimeSpan.FromSeconds(1.6);

    // countdowns and the like push the queue back by this much instead of a full gap
    private static readonly TimeSpan ShortGap = TimeSpan.FromSeconds(1);

    // SharedChatSystem.VoiceRange is 10, stay under it. a pad further away than this
    // cannot be heard at all, so the projection moves to the player instead of talking to a wall
    private const float AudibleRange = 8f;

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<TutorialSessionComponent, TransformComponent>();
        while (query.MoveNext(out var player, out var session, out var xform))
        {
            if (!_players.TryGetSessionByEntity(player, out _))
                continue;

            EnsureMentor(player, session, xform);
            FollowNearestPad(player, session, xform);
            LatchGates(session, xform);
            PumpSpeech(session, xform);
        }
    }

    /// <summary>Quips, stuck hints, death lines. Said as soon as the queue reaches them.</summary>
    public void Enqueue(EntityUid player, IEnumerable<LocId> lines)
    {
        if (!TryGetMentor(player, out var mentorComp))
            return;

        var quips = new List<TutorialSpeechLine>();
        foreach (var line in lines)
        {
            var text = Loc.GetString(line);
            if (!string.IsNullOrWhiteSpace(text))
                quips.Add(new TutorialSpeechLine { Text = text });
        }

        if (quips.Count == 0)
            return;

        // slipping on a banana and hearing about it eight seconds later is worse than useless,
        // so quips cut ahead of whatever briefing is still waiting
        var pending = mentorComp.SpeechQueue.ToList();
        mentorComp.SpeechQueue.Clear();

        foreach (var quip in quips)
            mentorComp.SpeechQueue.Enqueue(quip);

        foreach (var line in pending)
            mentorComp.SpeechQueue.Enqueue(line);

        mentorComp.NextSpeak = Sooner(mentorComp.NextSpeak, _timing.CurTime + ShortGap);
    }

    /// <summary>
    /// Step briefing. Every line waits until the trainee is near <paramref name="gateAnchor"/>,
    /// so walking out of the previous room no longer triggers the next lecture.
    /// </summary>
    public void EnqueueStep(
        EntityUid player,
        IEnumerable<LocId> lines,
        string? gateAnchor,
        float gateRange,
        float gateHoldSeconds)
    {
        if (!TryGetMentor(player, out var mentorComp))
            return;

        // whatever the previous step still had queued is stale now - the player already moved on.
        // without this the queue drifts further behind every room until she narrates the past
        DropPendingBriefing(mentorComp);

        mentorComp.NextSpeak = Later(mentorComp.NextSpeak, _timing.CurTime + StepSettle);

        foreach (var line in lines)
            QueueLine(mentorComp, Loc.GetString(line), gateAnchor, gateRange, gateHoldSeconds, fromStep: true);
    }

    public void DropBriefing(EntityUid player)
    {
        if (TryGetMentor(player, out var mentorComp))
            DropPendingBriefing(mentorComp);
    }

    public void EnqueueRaw(EntityUid player, string text)
    {
        if (!TryGetMentor(player, out var mentorComp))
            return;

        QueueLine(mentorComp, text, null, 0f, 0f, fromStep: false);
    }

    /// <summary>
    /// Skips the queue and the speak gap. Only for lines that are worthless late - a countdown
    /// stuck behind three sentences arrives after the thing it was counting down to.
    /// </summary>
    public void SpeakNow(EntityUid player, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (!TryComp<TutorialSessionComponent>(player, out var session))
            return;

        EnsureMentor(player, session, Transform(player));

        if (session.MentorUid is not { } mentor || Deleted(mentor))
            return;

        Speak(mentor, text);

        if (TryComp<TutorialMentorComponent>(mentor, out var mentorComp))
            mentorComp.NextSpeak = Later(mentorComp.NextSpeak, _timing.CurTime + ShortGap);
    }

    public void Cleanup(TutorialSessionComponent session)
    {
        if (session.MentorUid is { } mentor && TryComp<TutorialMentorComponent>(mentor, out var comp))
            SetPadActive(comp.CurrentPad, false);

        if (session.MentorUid is { } uid && !Deleted(uid))
            QueueDel(uid);

        session.MentorUid = null;
    }

    private bool TryGetMentor(EntityUid player, out TutorialMentorComponent mentorComp)
    {
        mentorComp = default!;

        if (!TryComp<TutorialSessionComponent>(player, out var session))
            return false;

        EnsureMentor(player, session, Transform(player));

        return session.MentorUid is { } mentor && TryComp(mentor, out mentorComp!);
    }

    /// <summary>Throws away unsaid step lines, keeps quips - those react to what just happened.</summary>
    private static void DropPendingBriefing(TutorialMentorComponent mentorComp)
    {
        if (mentorComp.SpeechQueue.Count == 0)
            return;

        var kept = mentorComp.SpeechQueue.Where(l => !l.FromStep).ToList();
        if (kept.Count == mentorComp.SpeechQueue.Count)
            return;

        mentorComp.SpeechQueue.Clear();
        foreach (var line in kept)
            mentorComp.SpeechQueue.Enqueue(line);
    }

    private void QueueLine(
        TutorialMentorComponent mentorComp,
        string text,
        string? gateAnchor,
        float gateRange,
        float gateHoldSeconds,
        bool fromStep)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        mentorComp.SpeechQueue.Enqueue(new TutorialSpeechLine
        {
            Text = text,
            GateAnchor = string.IsNullOrWhiteSpace(gateAnchor) ? null : gateAnchor,
            GateRange = gateRange,
            GateExpiresAt = _timing.CurTime + TimeSpan.FromSeconds(gateHoldSeconds),
            FromStep = fromStep,
        });
    }

    private void EnsureMentor(EntityUid player, TutorialSessionComponent session, TransformComponent xform)
    {
        if (session.MentorUid is { } existing && !Deleted(existing))
            return;

        var coords = xform.Coordinates;
        if (TryFindNearestPad(player, xform, out var pad, out var padXform))
            coords = padXform.Coordinates;

        var mentor = Spawn(MentorProto, coords);
        var mentorComp = EnsureComp<TutorialMentorComponent>(mentor);
        mentorComp.OwnerPlayer = player;
        mentorComp.CurrentPad = pad;
        mentorComp.NextSpeak = _timing.CurTime + FirstSpeakDelay;

        session.MentorUid = mentor;
        SetPadActive(pad, true);
    }

    private void FollowNearestPad(EntityUid player, TutorialSessionComponent session, TransformComponent xform)
    {
        if (session.MentorUid is not { } mentor || Deleted(mentor))
            return;

        if (!TryComp<TutorialMentorComponent>(mentor, out var mentorComp))
            return;

        if (!TryFindNearestPad(player, xform, out var pad, out var padXform))
            return;

        if (mentorComp.CurrentPad == pad)
            return;

        SetPadActive(mentorComp.CurrentPad, false);
        mentorComp.CurrentPad = pad;
        SetPadActive(pad, true);

        _transform.SetCoordinates(mentor, padXform.Coordinates);
    }

    /// <summary>
    /// Opens every queued gate whose anchor the trainee is standing near right now. Runs each tick,
    /// not only when a line is due, so a player who walks straight through the spot still gets told.
    /// </summary>
    private void LatchGates(TutorialSessionComponent session, TransformComponent xform)
    {
        if (session.MentorUid is not { } mentor || Deleted(mentor))
            return;

        if (!TryComp<TutorialMentorComponent>(mentor, out var mentorComp))
            return;

        foreach (var line in mentorComp.SpeechQueue)
        {
            if (line.GateAnchor is not { } anchorId)
                continue;

            if (_timing.CurTime >= line.GateExpiresAt || IsNearAnchor(session, xform, anchorId, line.GateRange))
                line.GateAnchor = null;
        }
    }

    private void PumpSpeech(TutorialSessionComponent session, TransformComponent xform)
    {
        if (session.MentorUid is not { } mentor || Deleted(mentor))
            return;

        if (!TryComp<TutorialMentorComponent>(mentor, out var mentorComp))
            return;

        if (mentorComp.SpeechQueue.Count == 0)
            return;

        if (_timing.CurTime < mentorComp.NextSpeak)
            return;

        // head of the queue holds back everything behind it, order matters more than speed
        if (mentorComp.SpeechQueue.Peek().GateAnchor != null)
            return;

        EnsureAudible(mentor, xform);

        var line = mentorComp.SpeechQueue.Dequeue();
        mentorComp.NextSpeak = _timing.CurTime + GapAfter(line.Text);

        // freeze steps need to know she got past the anchor gate and actually started.
        // a stuck hint arriving while the briefing is still gated must not arm it
        if (line.FromStep)
            session.Flags.Add(SpokeFlag);
        else
            mentorComp.QuipDoneAt = mentorComp.NextSpeak;

        Speak(mentor, line.Text);
    }

    /// <summary>Still talking, or still inside the reading gap after the last line.</summary>
    public bool IsSpeaking(EntityUid player)
    {
        if (!TryComp<TutorialSessionComponent>(player, out var session))
            return false;

        if (session.MentorUid is not { } mentor || !TryComp<TutorialMentorComponent>(mentor, out var mentorComp))
            return false;

        return mentorComp.SpeechQueue.Count > 0 || _timing.CurTime < mentorComp.NextSpeak;
    }

    /// <summary>A reaction line is still queued, or the last one said is still being read.</summary>
    public bool QuipsPending(EntityUid player)
    {
        if (!TryComp<TutorialSessionComponent>(player, out var session))
            return false;

        if (session.MentorUid is not { } mentor || !TryComp<TutorialMentorComponent>(mentor, out var mentorComp))
            return false;

        return mentorComp.SpeechQueue.Any(l => !l.FromStep) || _timing.CurTime < mentorComp.QuipDoneAt;
    }

    private static TimeSpan GapAfter(string text)
    {
        var seconds = GapBaseSeconds + text.Length * GapSecondsPerChar;
        return TimeSpan.FromSeconds(Math.Clamp(seconds, GapMinSeconds, GapMaxSeconds));
    }

    private void Speak(EntityUid mentor, string text)
    {
        _chat.TrySendInGameICMessage(
            mentor,
            text,
            InGameICChatType.Speak,
            hideChat: false,
            hideLog: true,
            ignoreActionBlocker: true);
    }

    /// <summary>
    /// Long stretches of the map have no holopad. Speaking from one 50 tiles away just drops the
    /// line, so project next to the trainee instead - being odd beats being silent.
    /// </summary>
    private void EnsureAudible(EntityUid mentor, TransformComponent playerXform)
    {
        var delta = _transform.GetWorldPosition(mentor) - _transform.GetWorldPosition(playerXform);
        if (delta.Length() <= AudibleRange)
            return;

        _transform.SetCoordinates(mentor, playerXform.Coordinates);
    }

    private bool IsNearAnchor(
        TutorialSessionComponent session,
        TransformComponent xform,
        string anchorId,
        float range)
    {
        if (xform.GridUid is not { } grid)
            return true;

        var playerPos = _transform.GetWorldPosition(xform);
        var rangeSq = range * range;

        if (session.Anchors.TryGetValue(anchorId, out var known)
            && !Deleted(known)
            && TryComp(known, out TransformComponent? knownXform)
            && knownXform.GridUid == grid
            && (_transform.GetWorldPosition(knownXform) - playerPos).LengthSquared() <= rangeSq)
        {
            return true;
        }

        // anchors spawned mid flow never made it into the dict, so sweep the grid too
        var query = EntityQueryEnumerator<TutorialAnchorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var anchor, out var ax))
        {
            if (anchor.AnchorId != anchorId || ax.GridUid != grid || Deleted(uid))
                continue;

            if ((_transform.GetWorldPosition(ax) - playerPos).LengthSquared() <= rangeSq)
                return true;
        }

        return false;
    }

    private bool TryFindNearestPad(
        EntityUid player,
        TransformComponent playerXform,
        out EntityUid pad,
        out TransformComponent padXform)
    {
        pad = default;
        padXform = default!;

        if (playerXform.GridUid is not { } grid)
            return false;

        var playerPos = _transform.GetWorldPosition(playerXform);
        var best = float.MaxValue;
        var found = false;

        var query = EntityQueryEnumerator<TutorialHoloPointComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var hx))
        {
            if (hx.GridUid != grid)
                continue;

            var dist = (_transform.GetWorldPosition(hx) - playerPos).LengthSquared();
            if (dist >= best)
                continue;

            best = dist;
            pad = uid;
            padXform = hx;
            found = true;
        }

        return found;
    }

    private void SetPadActive(EntityUid? pad, bool active)
    {
        if (pad is not { } uid || Deleted(uid))
            return;

        _appearance.SetData(uid, TutorialHoloPointVisuals.Active, active);

        if (TryComp<PointLightComponent>(uid, out var light))
            _lights.SetEnabled(uid, active, light);

        if (TryComp<TutorialHoloPointComponent>(uid, out var point))
            point.Projection = active ? TryGetMentorOnPad(uid) : null;
    }

    private EntityUid? TryGetMentorOnPad(EntityUid pad)
    {
        var query = EntityQueryEnumerator<TutorialMentorComponent>();
        while (query.MoveNext(out var uid, out var mentor))
        {
            if (mentor.CurrentPad == pad)
                return uid;
        }

        return null;
    }

    private static TimeSpan Later(TimeSpan a, TimeSpan b) => a > b ? a : b;

    private static TimeSpan Sooner(TimeSpan a, TimeSpan b) => a < b ? a : b;
}
