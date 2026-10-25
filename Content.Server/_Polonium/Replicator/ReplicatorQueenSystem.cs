using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Shared._Impstation.Replicator;
using Content.Shared._Polonium.Replicator;
using Content.Shared.Actions;
using Content.Shared.Chat;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Polonium.Replicator;

/// <summary>
/// Keeps every hive under a queen that is alive and has a player, and keeps the nest action with that queen.
/// A queen whose player has only disconnected keeps the crown for a while.
/// </summary>
public sealed partial class ReplicatorQueenSystem : EntitySystem
{
    private static readonly Color MessageColor = Color.FromHex("#e455c4");

    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    private readonly List<Entity<ReplicatorComponent>> _hive = new();
    private readonly List<Entity<ReplicatorComponent>> _living = new();
    private readonly List<Entity<ReplicatorComponent>> _heirs = new();
    private readonly List<Entity<ReplicatorComponent>> _overdue = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<ReplicatorComponent, PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<ReplicatorComponent, PlayerAttachedEvent>(OnPlayerAttached);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _overdue.Clear();
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ReplicatorQueenAbsentComponent, ReplicatorComponent>();
        while (query.MoveNext(out var uid, out var absent, out var replicator))
        {
            if (now >= absent.Deadline)
                _overdue.Add((uid, replicator));
        }

        foreach (var queen in _overdue)
        {
            RemComp<ReplicatorQueenAbsentComponent>(queen);

            if (queen.Comp.Queen && queen.Comp.MyNest is { } nest)
                UpdateHive(nest);
        }
    }

    private void OnMobStateChanged(Entity<ReplicatorComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead && ent.Comp.Queen && ent.Comp.MyNest is { } nest)
            UpdateHive(nest);
    }

    private void OnPlayerDetached(Entity<ReplicatorComponent> ent, ref PlayerDetachedEvent args)
    {
        if (!ent.Comp.Queen || ent.Comp.MyNest is not { } nest)
            return;

        // The mind stays in the body on a disconnect and leaves it when the player ghosts.
        if (!TerminatingOrDeleted(ent.Owner)
            && _mobState.IsAlive(ent)
            && TryComp<MindContainerComponent>(ent, out var mind)
            && mind.HasMind)
        {
            EnsureComp<ReplicatorQueenAbsentComponent>(ent).Deadline = _timing.CurTime + ent.Comp.QueenAbsenceGrace;
            return;
        }

        UpdateHive(nest);
    }

    private void OnPlayerAttached(Entity<ReplicatorComponent> ent, ref PlayerAttachedEvent args)
    {
        RemComp<ReplicatorQueenAbsentComponent>(ent);

        if (ent.Comp.MyNest is { } nest)
            UpdateHive(nest);
    }

    /// <summary>
    /// Crowns a new queen if the hive of this nest has none that is alive and played, then hands the nest action
    /// to the queen of a hive without a nest. The nest may already be gone, its id still groups the hive.
    /// </summary>
    public void UpdateHive(EntityUid nest, bool nestLost = false)
    {
        // Players leave their bodies one by one when the round restarts.
        if (_ticker.RunLevel == GameRunLevel.PreRoundLobby)
            return;

        _hive.Clear();
        _living.Clear();
        _heirs.Clear();
        Entity<ReplicatorComponent>? reigning = null;

        var query = EntityQueryEnumerator<ReplicatorComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.MyNest != nest || TerminatingOrDeleted(uid))
                continue;

            _hive.Add((uid, comp));

            if (!_mobState.IsAlive(uid))
                continue;

            _living.Add((uid, comp));

            if (!_player.TryGetSessionByEntity(uid, out _))
            {
                if (comp.Queen && HasComp<ReplicatorQueenAbsentComponent>(uid))
                    reigning = (uid, comp);

                continue;
            }

            if (comp.Queen)
                reigning = (uid, comp);
            else
                _heirs.Add((uid, comp));
        }

        var crowned = reigning == null;
        if (reigning is not { } queen)
        {
            if (_heirs.Count == 0)
                return;

            queen = PickHeir();
        }

        foreach (var member in _hive)
        {
            if (member.Owner == queen.Owner)
                continue;

            SetQueen(member, false);
            RemoveNestAction(member);
        }

        SetQueen(queen, true);

        var nestless = nestLost || !HasNest(nest);
        if (nestless)
        {
            queen.Comp.RelatedReplicators = new HashSet<Entity<ReplicatorComponent>>(_living);
            queen.Comp.RelatedReplicators.Remove(queen);
            _actions.AddAction(queen, ref queen.Comp.NestAction, queen.Comp.SpawnNewNestAction);
        }
        else
        {
            RemoveNestAction(queen);
        }

        if (crowned)
            Announce(queen, nestless ? "replicator-queen-crowned-nestless" : "replicator-queen-crowned");
        else if (nestLost)
            Announce(queen, "replicator-queen-nest-lost");
    }

    /// <summary>
    /// Gives the nest action to a queen whose hive has no nest and takes it from everyone else.
    /// </summary>
    public void RefreshNestAction(Entity<ReplicatorComponent> ent)
    {
        if (ent.Comp.Queen && !HasNest(ent.Comp.MyNest))
            _actions.AddAction(ent, ref ent.Comp.NestAction, ent.Comp.SpawnNewNestAction);
        else
            RemoveNestAction(ent);
    }

    private Entity<ReplicatorComponent> PickHeir()
    {
        var swarmers = _heirs.FindAll(heir => !HasComp<ReplicatorColossusComponent>(heir));
        return _random.Pick(swarmers.Count > 0 ? swarmers : _heirs);
    }

    private void SetQueen(Entity<ReplicatorComponent> ent, bool queen)
    {
        if (ent.Comp.Queen == queen)
            return;

        ent.Comp.Queen = queen;
        Dirty(ent);

        if (!queen)
            RemComp<ReplicatorQueenAbsentComponent>(ent);
    }

    private void RemoveNestAction(Entity<ReplicatorComponent> ent)
    {
        if (ent.Comp.NestAction is not { } action)
            return;

        ent.Comp.NestAction = null;

        if (TerminatingOrDeleted(action))
            return;

        _actions.RemoveAction(ent.Owner, action);
        QueueDel(action);
    }

    private bool HasNest(EntityUid? nest)
    {
        return nest is { } uid && !TerminatingOrDeleted(uid) && HasComp<ReplicatorNestComponent>(uid);
    }

    private void Announce(Entity<ReplicatorComponent> queen, string message)
    {
        var name = FormattedMessage.EscapeText(Name(queen));

        foreach (var member in _living)
        {
            if (!_player.TryGetSessionByEntity(member, out var session))
                continue;

            var text = member.Owner == queen.Owner
                ? Loc.GetString(message + "-self")
                : Loc.GetString(message, ("queen", name));
            var wrapped = Loc.GetString("chat-manager-server-wrap-message", ("message", text));
            _chat.ChatMessageToOne(ChatChannel.Server, text, wrapped, default, false, session.Channel, MessageColor);
        }
    }
}
