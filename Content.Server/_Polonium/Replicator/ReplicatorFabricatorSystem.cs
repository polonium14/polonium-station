using Content.Shared._Impstation.Replicator;
using Content.Shared._Impstation.SpawnedFromTracker;
using Content.Shared._Polonium.Replicator;
using Content.Shared.Examine;
using Content.Shared.Maps;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Polonium.Replicator;

public sealed partial class ReplicatorFabricatorSystem : EntitySystem
{
    private static readonly SpriteSpecifier VerbIcon =
        new SpriteSpecifier.Rsi(new ResPath("/Textures/_Impstation/Mobs/Replicator/replicator.rsi"), "icon");

    private static readonly Direction[] HatchDirections = [Direction.South, Direction.East, Direction.West, Direction.North];

    private const CollisionGroup HatchBlockers = CollisionGroup.Impassable | CollisionGroup.LowImpassable;

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ReplicatorHiveSystem _hive = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TurfSystem _turf = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorFabricatorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ReplicatorFabricatorComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<ReplicatorFabricatorComponent, ExaminedEvent>(OnExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ReplicatorFabricatorComponent>();
        while (query.MoveNext(out var uid, out var fabricator))
        {
            if (fabricator.FinishTime is { } finish && _timing.CurTime >= finish)
                Finish((uid, fabricator));
            else if (fabricator.WaitingShell is { } shell && TerminatingOrDeleted(shell))
                Reset((uid, fabricator));
        }
    }

    private void OnMapInit(Entity<ReplicatorFabricatorComponent> ent, ref MapInitEvent args)
    {
        _appearance.SetData(ent, ReplicatorFabricatorVisuals.State, ReplicatorFabricatorState.Idle);
    }

    private void OnGetVerbs(Entity<ReplicatorFabricatorComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !HasComp<ReplicatorComponent>(args.User))
            return;

        var user = args.User;
        var verb = new AlternativeVerb
        {
            Text = Loc.GetString("replicator-fabricator-verb", ("cost", ReplicatorHiveSystem.ToSheets(ent.Comp.ShellCost))),
            Icon = VerbIcon,
            Act = () => TryStart(ent, user),
        };

        if (IsBusy(ent.Comp))
        {
            verb.Disabled = true;
            verb.Message = Loc.GetString("replicator-fabricator-busy");
        }

        args.Verbs.Add(verb);
    }

    private void OnExamined(Entity<ReplicatorFabricatorComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (ent.Comp.FinishTime is { } finish)
        {
            var left = Math.Max(0, (int)Math.Ceiling((finish - _timing.CurTime).TotalSeconds));
            args.PushMarkup(Loc.GetString("replicator-fabricator-examine-working", ("seconds", left)));
        }
        else if (ent.Comp.WaitingShell != null)
        {
            args.PushMarkup(Loc.GetString("replicator-fabricator-examine-ready"));
        }
        else
        {
            args.PushMarkup(Loc.GetString("replicator-fabricator-examine-idle"));
        }
    }

    private void TryStart(Entity<ReplicatorFabricatorComponent> ent, EntityUid user)
    {
        if (IsBusy(ent.Comp))
        {
            _popup.PopupEntity(Loc.GetString("replicator-fabricator-busy"), ent, user);
            return;
        }

        if (!_hive.TryGetHive(user, out var hive))
        {
            _popup.PopupEntity(Loc.GetString("replicator-construction-fail-no-nest"), ent, user);
            return;
        }

        var limit = GetHiveLimit(hive);
        if (CountHive(hive) >= limit)
        {
            _popup.PopupEntity(Loc.GetString("replicator-fabricator-limit", ("limit", limit)), ent, user);
            return;
        }

        if (!_hive.TrySpend(hive, ent.Comp.ShellCost))
        {
            _popup.PopupEntity(Loc.GetString("replicator-construction-fail-budget",
                ("cost", ReplicatorHiveSystem.ToSheets(ent.Comp.ShellCost))), ent, user);
            return;
        }

        ent.Comp.Nest = hive;
        ent.Comp.FinishTime = _timing.CurTime + ent.Comp.ProductionTime;
        _appearance.SetData(ent, ReplicatorFabricatorVisuals.State, ReplicatorFabricatorState.Working);
        _popup.PopupEntity(Loc.GetString("replicator-fabricator-started"), ent, user);
    }

    private void Finish(Entity<ReplicatorFabricatorComponent> ent)
    {
        ent.Comp.FinishTime = null;

        if (ent.Comp.Nest is not { } nest || !TryComp<ReplicatorNestComponent>(nest, out var nestComp) || TerminatingOrDeleted(nest))
        {
            Reset(ent);
            return;
        }

        var shell = Spawn(ent.Comp.Shell, GetHatchCoordinates(ent));
        EnsureComp<SpawnedFromTrackerComponent>(shell).SpawnedFrom = nest;
        nestComp.UnclaimedSpawners.Add(shell);

        ent.Comp.WaitingShell = shell;
        _appearance.SetData(ent, ReplicatorFabricatorVisuals.State, ReplicatorFabricatorState.Ready);
        _audio.PlayPvs(ent.Comp.FinishSound, ent);
    }

    private void Reset(Entity<ReplicatorFabricatorComponent> ent)
    {
        ent.Comp.FinishTime = null;
        ent.Comp.WaitingShell = null;
        ent.Comp.Nest = null;
        _appearance.SetData(ent, ReplicatorFabricatorVisuals.State, ReplicatorFabricatorState.Idle);
    }

    private static bool IsBusy(ReplicatorFabricatorComponent fabricator)
    {
        return fabricator.FinishTime != null || fabricator.WaitingShell != null;
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

        var query = EntityQueryEnumerator<ReplicatorFabricatorComponent>();
        while (query.MoveNext(out _, out var fabricator))
        {
            if (fabricator.Nest == hive.Owner && fabricator.FinishTime != null)
                count++;
        }

        return count;
    }
}
