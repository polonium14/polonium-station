using Content.Server.Lathe.Components;
using Content.Shared._Impstation.Replicator;
using Content.Shared._Impstation.SpawnedFromTracker;
using Content.Shared._Polonium.Lathe;
using Content.Shared._Polonium.Replicator;
using Content.Shared.CCVar;
using Content.Shared.Examine;
using Content.Shared.Lathe;
using Content.Shared.Maps;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Replicator;

public sealed partial class ReplicatorFabricatorSystem : EntitySystem
{
    private static readonly Direction[] HatchDirections = [Direction.South, Direction.East, Direction.West, Direction.North];

    private const CollisionGroup HatchBlockers = CollisionGroup.Impassable | CollisionGroup.LowImpassable;

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ReplicatorHiveSystem _hive = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;

    private readonly List<EntityUid> _claimed = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorFabricatorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ReplicatorFabricatorComponent, ActivatableUIOpenAttemptEvent>(OnUiOpenAttempt);
        SubscribeLocalEvent<ReplicatorFabricatorComponent, LatheQueueAttemptEvent>(OnQueueAttempt);
        SubscribeLocalEvent<ReplicatorFabricatorComponent, LatheGetResultEvent>(OnLatheResult);
        SubscribeLocalEvent<ReplicatorFabricatorComponent, ExaminedEvent>(OnExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ReplicatorFabricatorComponent, LatheComponent>();
        while (query.MoveNext(out var uid, out var fabricator, out var lathe))
        {
            _claimed.Clear();
            foreach (var shell in fabricator.WaitingShells)
            {
                if (TerminatingOrDeleted(shell))
                    _claimed.Add(shell);
            }

            foreach (var shell in _claimed)
            {
                fabricator.WaitingShells.Remove(shell);
            }

            var state = lathe.CurrentRecipe != null
                ? ReplicatorFabricatorState.Working
                : fabricator.WaitingShells.Count > 0
                    ? ReplicatorFabricatorState.Ready
                    : ReplicatorFabricatorState.Idle;

            _appearance.SetData(uid, ReplicatorFabricatorVisuals.State, state);
        }
    }

    private void OnMapInit(Entity<ReplicatorFabricatorComponent> ent, ref MapInitEvent args)
    {
        _appearance.SetData(ent, ReplicatorFabricatorVisuals.State, ReplicatorFabricatorState.Idle);
    }

    private void OnUiOpenAttempt(Entity<ReplicatorFabricatorComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!HasComp<ReplicatorComponent>(args.User))
            args.Cancel();
    }

    private void OnQueueAttempt(Entity<ReplicatorFabricatorComponent> ent, ref LatheQueueAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!_hive.TryGetHive(args.Actor, out var hive))
        {
            _popup.PopupEntity(Loc.GetString("replicator-construction-fail-no-nest"), ent, args.Actor, PopupType.SmallCaution);
            args.Cancelled = true;
            return;
        }

        var limit = GetHiveLimit(hive);
        if (CountHive(hive) + args.Quantity > limit)
        {
            _popup.PopupEntity(Loc.GetString("replicator-fabricator-limit", ("limit", limit)), ent, args.Actor, PopupType.SmallCaution);
            args.Cancelled = true;
            return;
        }

        ent.Comp.Nest = hive;
    }

    private void OnLatheResult(Entity<ReplicatorFabricatorComponent> ent, ref LatheGetResultEvent args)
    {
        var shell = args.ResultItem;

        if (ent.Comp.Nest is not { } nest || TerminatingOrDeleted(nest) || !TryComp<ReplicatorNestComponent>(nest, out var nestComp))
        {
            QueueDel(shell);
            return;
        }

        _transform.SetCoordinates(shell, GetHatchCoordinates(ent));
        EnsureComp<SpawnedFromTrackerComponent>(shell).SpawnedFrom = nest;
        nestComp.UnclaimedSpawners.Add(shell);

        ent.Comp.WaitingShells.Add(shell);
        _audio.PlayPvs(ent.Comp.FinishSound, ent);
    }

    private void OnExamined(Entity<ReplicatorFabricatorComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (TryComp<LatheComponent>(ent, out var lathe)
            && lathe.CurrentRecipe != null
            && TryComp<LatheProducingComponent>(ent, out var producing))
        {
            var finish = producing.StartTime + producing.ProductionLength;
            var left = Math.Max(0, (int)Math.Ceiling((finish - _timing.CurTime).TotalSeconds));
            args.PushMarkup(Loc.GetString("replicator-fabricator-examine-working", ("seconds", left)));
        }
        else if (ent.Comp.WaitingShells.Count > 0)
        {
            args.PushMarkup(Loc.GetString("replicator-fabricator-examine-ready"));
        }
        else
        {
            args.PushMarkup(Loc.GetString("replicator-fabricator-examine-idle"));
        }
    }

    // The fabricator is solid, a replicator spawned on top of it would be stuck inside.
    private EntityCoordinates GetHatchCoordinates(EntityUid fabricator)
    {
        var xform = Transform(fabricator);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return xform.Coordinates;

        var origin = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        foreach (var dir in HatchDirections)
        {
            var indices = origin.Offset(dir);
            if (!_map.TryGetTileRef(gridUid, grid, indices, out var tile)
                || _turf.IsSpace(tile)
                || _turf.IsTileBlocked(tile, HatchBlockers))
                continue;

            return _map.GridTileToLocal(gridUid, grid, indices);
        }

        return xform.Coordinates;
    }

    private int GetHiveLimit(Entity<ReplicatorHiveComponent> hive)
    {
        if (_cfg.GetCVar(CCVars.ReplicatorIgnorePlayerCount))
            return hive.Comp.MaxReplicators;

        var perPlayers = hive.Comp.PlayersPerReplicator > 0 ? _player.PlayerCount / hive.Comp.PlayersPerReplicator : 0;
        return Math.Clamp(perPlayers, hive.Comp.MinReplicators, hive.Comp.MaxReplicators);
    }

    private int CountHive(Entity<ReplicatorHiveComponent, ReplicatorNestComponent> hive)
    {
        var count = 0;

        foreach (var minion in hive.Comp2.SpawnedMinions)
        {
            if (!TerminatingOrDeleted(minion) && !_mobState.IsDead(minion))
                count++;
        }

        foreach (var shell in hive.Comp2.UnclaimedSpawners)
        {
            if (!TerminatingOrDeleted(shell))
                count++;
        }

        var query = EntityQueryEnumerator<ReplicatorFabricatorComponent, LatheComponent>();
        while (query.MoveNext(out _, out var fabricator, out var lathe))
        {
            if (fabricator.Nest != hive.Owner)
                continue;

            if (lathe.CurrentRecipe != null)
                count++;

            foreach (var batch in lathe.Queue)
            {
                count += batch.ItemsRequested - batch.ItemsPrinted;
            }
        }

        return count;
    }
}
