using Content.Server.Construction;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared._Polonium.Tutorial;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared._Polonium.Tutorial.Prototypes;
using Content.Shared.CCVar;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Standing;
using Content.Shared.Tools.Components;
using Content.Shared.Wall;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Tutorial;

public sealed partial class TutorialSystem : SharedTutorialSystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private TutorialActionExecutor _actions = default!;
    [Dependency] private TutorialMentorSystem _mentor = default!;
    [Dependency] private SolitarySpawningSystem _solitary = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private StandingStateSystem _standing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TutorialStartRequestedEvent>(OnStartRequested);
        SubscribeLocalEvent<TutorialSessionComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<TutorialSessionComponent, BeforeDamageChangedEvent>(OnPlayerDamage);
        SubscribeLocalEvent<TutorialSessionComponent, MobStateChangedEvent>(OnPlayerMobState);
        // Construction already took that pair
        SubscribeLocalEvent<WallComponent, InteractUsingEvent>(OnWallUsing,
            before: [typeof(ConstructionSystem)]);
        SubscribeLocalEvent<WallMountComponent, InteractUsingEvent>(OnWindowUsing,
            before: [typeof(ConstructionSystem)]);
        SubscribeLocalEvent<TutorialSealedComponent, InteractUsingEvent>(OnSealedUsing,
            before: [typeof(ConstructionSystem)]);
        SubscribeNetworkEvent<TutorialRestartRequestedEvent>(OnRestartRequested);
        SubscribeNetworkEvent<TutorialStartPracticalEvent>(OnStartPractical);
        SubscribeNetworkEvent<TutorialFinaleChoiceEvent>(OnFinaleChoice);
        SubscribeLocalEvent<PlayerJoinedLobbyEvent>(OnPlayerJoinedLobby);
        _player.PlayerStatusChanged += OnPlayerStatus;
    }

    public override void Shutdown()
    {
        _player.PlayerStatusChanged -= OnPlayerStatus;
        base.Shutdown();
    }

    public void ForceAdvance(Entity<TutorialSessionComponent?> player)
    {
        if (!Resolve(player, ref player.Comp, false))
            return;

        AdvanceStep((player.Owner, player.Comp));
    }

    public void ForceStartFlow(EntityUid player, ProtoId<TutorialFlowPrototype> flowId) =>
        StartFlow(player, flowId);

    private void OnStartRequested(TutorialStartRequestedEvent ev)
    {
        StartFlow(ev.Player, ev.Flow);
    }

    private void OnStartPractical(TutorialStartPracticalEvent ev, EntitySessionEventArgs args)
    {
        if (ReadIntroMode() != IntroTutorial)
            return;

        _solitary.TryJoinFromLobby(args.SenderSession);
    }

    private void OnPlayerStatus(object? sender, SessionStatusEventArgs ev)
    {
        if (ev.NewStatus != SessionStatus.Connected)
            return;

        if (ReadIntroMode() != IntroMain)
            return;

        if (string.IsNullOrEmpty(_cfg.GetCVar(CCVars.IntroSolitaryServerConnectionString)))
            return;

        SendCompletionStatus(ev.Session);
    }

    private void OnPlayerJoinedLobby(PlayerJoinedLobbyEvent ev)
    {
        if (ReadIntroMode() == IntroTutorial)
            _solitary.TryJoinFromLobby(ev.PlayerSession);
    }

    private async void SendCompletionStatus(ICommonSession session)
    {
        var completed = false;
        try
        {
            (completed, _) = await _db.GetTutorialCompletion(session.UserId);
        }
        catch (Exception e)
        {
            Log.Error($"Tutorial: failed to read completion for {session.UserId}: {e}");
        }

        if (session.Status == SessionStatus.Disconnected)
            return;

        RaiseNetworkEvent(new TutorialPlayerCompletionEvent(completed), session);
    }

    private void OnRestartRequested(TutorialRestartRequestedEvent ev, EntitySessionEventArgs args)
    {
        if (ReadIntroMode() == IntroNone)
            return;

        var session = args.SenderSession;

        if (_solitary.TryRestartTutorial(session))
            return;

        // tutorialforcestart / already in a flow on a dirty map - replay in place
        if (session.AttachedEntity is not { } mob || !TryComp<TutorialSessionComponent>(mob, out var tut))
            return;

        StartFlow(mob, tut.Flow);
    }

    private void StartFlow(EntityUid player, ProtoId<TutorialFlowPrototype> flowId)
    {
        if (ReadIntroMode() == IntroNone)
            return;

        if (!_player.TryGetSessionByEntity(player, out _))
            return;

        if (HasComp<TutorialNpcComponent>(player))
            return;

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
        session.FlowStartedAt = DateTime.UtcNow;
        session.Anchors = ResolveAnchorsOnGrid(player);

        EnsureComp<PacifiedComponent>(player);

        _actions.BoltAllAirlocks(player);
        _actions.PowerAllDevices(player);

        var start = ResolveDebugStartStep(flow, out var startRoom);
        if (start > 0)
        {
            FastForwardBefore((player, session), flow, start);
            if (startRoom is not null)
                _actions.Teleport(player, startRoom);
        }

        EnterStep((player, session), start);
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

    private void OnShutdown(Entity<TutorialSessionComponent> ent, ref ComponentShutdown args)
    {
        _mentor.Cleanup(ent.Comp);

        // both of these are only ever maintained while a session exists. leaving them behind on
        // the last step means a player who cannot move and cannot swing, forever
        RemComp<TutorialFrozenComponent>(ent.Owner);
        RemComp<PacifiedComponent>(ent.Owner);
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
        ent.Comp.StepStartedAt = _timing.CurTime;
        ent.Comp.PendingAdvanceAt = null;
        ent.Comp.StuckHinted = false;
        Dirty(ent);

        _actions.ExecuteAll(ent.Owner, stepProto.OnEnter);
        _mentor.EnqueueStep(
            ent.Owner,
            stepProto.Speak,
            stepProto.SpeakAtAnchor,
            stepProto.SpeakAtRange,
            stepProto.SpeakHoldSeconds);

        if (stepProto.Finale)
            OnEnteredFinale(ent);

        Log.Debug($"Tutorial: {ToPrettyString(ent.Owner)} entered step '{stepId}' ({index + 1}/{flow.Steps.Count})");
    }

    private void OnEnteredFinale(Entity<TutorialSessionComponent> ent)
    {
        // the music is started by the client when the finale bubble shows, a server playlist here would cut it
        if (_player.TryGetSessionByEntity(ent.Owner, out var sess))
            RecordTutorialCompletion(sess, DateTime.UtcNow - ent.Comp.FlowStartedAt);
    }

    private void OnFinaleChoice(TutorialFinaleChoiceEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } player)
            return;

        if (!TryComp<TutorialSessionComponent>(player, out var session))
            return;

        if (!TryGetCurrentStep(session, out _, out var proto) || !proto.Finale)
            return;

        CompleteFlow((player, session), redial: ev.JoinServer);
    }

    private async void RecordTutorialCompletion(ICommonSession session, TimeSpan duration)
    {
        try
        {
            await _db.SetTutorialCompletion(session.UserId, duration);

            if (session.Status != SessionStatus.Disconnected)
                RaiseNetworkEvent(new TutorialPlayerCompletionEvent(true), session);
        }
        catch (Exception e)
        {
            Log.Error($"Tutorial: failed to save completion for {session.UserId}: {e}");
        }
    }

    private void CompleteFlow(Entity<TutorialSessionComponent> ent, bool redial = false)
    {
        Log.Debug($"Tutorial: {ToPrettyString(ent.Owner)} completed flow '{ent.Comp.Flow}'");

        if (redial)
            TryRedial(ent.Owner);

        ent.Comp.CurrentStep = null;
        ent.Comp.NavigationAnchor = null;
        Dirty(ent);

        RemComp<TutorialSessionComponent>(ent.Owner);
    }

    private void TryRedial(EntityUid player)
    {
        var address = _cfg.GetCVar(CCVars.IntroReturnServerConnectionString);
        if (string.IsNullOrWhiteSpace(address))
            return;

        if (!_player.TryGetSessionByEntity(player, out var session))
            return;

        RaiseNetworkEvent(
            new TutorialRedialEvent(address, Loc.GetString("tutorial-redial-message")),
            session);
    }

    private bool TryGetFlow(TutorialSessionComponent session, out TutorialFlowPrototype flow)
    {
        return _proto.TryIndex(session.Flow, out flow!);
    }

    public bool TryGetCurrentStep(
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

    private void OnWallUsing(EntityUid uid, WallComponent component, InteractUsingEvent args)
    {
        TryBlockHullUsing(uid, args);
    }

    private void OnWindowUsing(EntityUid uid, WallMountComponent component, InteractUsingEvent args)
    {
        TryBlockHullUsing(uid, args);
    }

    private void OnSealedUsing(Entity<TutorialSealedComponent> ent, ref InteractUsingEvent args)
    {
        args.Handled = true;
    }

    private void TryBlockHullUsing(EntityUid uid, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<TutorialSessionComponent>(args.User, out var session))
            return;

        if (!IsHullStructure(uid))
            return;

        // a cable coil clicks the wall to occupy that tile, it is not taking the wall apart
        if (!HasComp<ToolComponent>(args.Used))
            return;

        if (!TryBlockStructureAttack(args.User, session, uid))
            return;

        args.Handled = true;
    }

    private void OnPlayerDamage(Entity<TutorialSessionComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (!args.Damage.AnyPositive())
            return;

        if (!_thresholds.TryGetThresholdForState(ent.Owner, MobState.Critical, out var critAt) || critAt is null)
            return;

        if (!TryComp<DamageableComponent>(ent.Owner, out var dmg))
            return;

        if (_damageable.GetPositiveDamage((ent.Owner, dmg)).GetTotal() + args.Damage.GetTotal() < critAt.Value)
            return;

        args.Cancelled = true;
    }

    private void OnPlayerMobState(Entity<TutorialSessionComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState is MobState.Alive or MobState.Invalid)
            return;

        _damageable.ClearAllDamage(ent.Owner);
        _mobState.ChangeMobState(ent.Owner, MobState.Alive);
        _standing.Stand(ent.Owner);

        if (ent.Comp.NavigationAnchor is { } nav)
            _actions.Teleport(ent.Owner, nav);

        _mentor.Enqueue(ent.Owner, new LocId[] { "tutorial-holopad-quip-death" });
    }
}
