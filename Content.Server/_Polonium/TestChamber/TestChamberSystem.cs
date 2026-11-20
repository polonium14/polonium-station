using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Shared._Polonium.TestChamber;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.Doors.Components;
using Content.Shared.GameTicking;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.TestChamber;

/// <summary>
/// Test chamber without its own map: a console that drops kits for testing PRs around itself and removes them again.
/// </summary>
public sealed partial class TestChamberSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public static readonly EntProtoId ConsolePrototype = "PoloniumTestChamberConsole";

    private const CollisionGroup BlockingMask = CollisionGroup.Impassable | CollisionGroup.MidImpassable | CollisionGroup.HighImpassable;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
        SubscribeLocalEvent<TestChamberConsoleComponent, TestChamberSpawnKitMessage>(OnSpawnKitMessage);
        SubscribeLocalEvent<TestChamberConsoleComponent, TestChamberResetMessage>(OnResetMessage);
    }

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent ev)
    {
        if (!_cfg.GetCVar(CCVars.TestChamberConsole))
            return;

        var xform = Transform(ev.Mob);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        // One console per spawn area is enough, the next players join next to the same one
        var consoles = EntityQueryEnumerator<TestChamberConsoleComponent, TransformComponent>();
        while (consoles.MoveNext(out _, out var consoleXform))
        {
            if (consoleXform.GridUid == gridUid && _transform.InRange(consoleXform.Coordinates, xform.Coordinates, 10f))
                return;
        }

        var center = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        foreach (var tile in FreeTiles((gridUid, grid), center, 3))
        {
            Spawn(ConsolePrototype, _map.GridTileToLocal(gridUid, grid, tile));
            return;
        }

        Log.Warning($"No free tile for the test chamber console next to {ToPrettyString(ev.Mob)}");
    }

    private void OnSpawnKitMessage(Entity<TestChamberConsoleComponent> ent, ref TestChamberSpawnKitMessage args)
    {
        if (!CanUse(ent, args.Actor, out var session) || !_proto.TryIndex<TestChamberKitPrototype>(args.Kit, out var kit))
            return;

        if (SpawnKit(ent, kit, args.Actor) is not { } spawned)
        {
            _chat.DispatchServerMessage(session, Loc.GetString("test-chamber-no-space"));
            return;
        }

        _adminLog.Add(LogType.EntitySpawn, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} spawned test chamber kit {kit.ID} ({spawned.Count} entities)");

        var header = kit.Pr == null ? kit.Name : $"{kit.Name} ({kit.Pr})";
        _chat.DispatchServerMessage(session, kit.Description == null ? header : $"{header}: {kit.Description}");
    }

    private void OnResetMessage(Entity<TestChamberConsoleComponent> ent, ref TestChamberResetMessage args)
    {
        if (!CanUse(ent, args.Actor, out var session))
            return;

        var deleted = Reset();
        _adminLog.Add(LogType.EntityDelete, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} reset the test chamber ({deleted} entities)");
        _chat.DispatchServerMessage(session, Loc.GetString("test-chamber-reset-done", ("count", deleted)));
    }

    private bool CanUse(EntityUid console, EntityUid actor, out ICommonSession session)
    {
        session = default!;
        if (!TryComp<ActorComponent>(actor, out var actorComp))
            return false;

        session = actorComp.PlayerSession;
        if (_admin.HasAdminFlag(session, AdminFlags.Debug))
            return true;

        _chat.DispatchServerMessage(session, Loc.GetString("test-chamber-no-permission"));
        _ui.CloseUi(console, TestChamberUiKey.Key, actor);
        return false;
    }

    /// <summary>
    /// Drops the kit on free tiles around the console, closest first.
    /// Returns null if there is no free tile left.
    /// </summary>
    public List<EntityUid>? SpawnKit(Entity<TestChamberConsoleComponent> console, TestChamberKitPrototype kit, EntityUid? user = null)
    {
        var xform = Transform(console);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return null;

        var center = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var tiles = FreeTiles((gridUid, grid), center, console.Comp.Range).ToList();
        if (tiles.Count == 0)
            return null;

        var spawned = new List<EntityUid>();
        var index = 0;
        foreach (var (proto, amount) in kit.Entities)
        {
            var coords = _map.GridTileToLocal(gridUid, grid, tiles[Math.Min(index++, tiles.Count - 1)]);
            for (var i = 0; i < Math.Max(amount, 1); i++)
            {
                var uid = Spawn(proto, coords);
                EnsureComp<TestChamberSpawnedComponent>(uid);
                spawned.Add(uid);
            }
        }

        RaiseLocalEvent(new TestChamberKitSpawnedEvent(kit.ID, console, user, spawned));
        return spawned;
    }

    /// <summary>
    /// Deletes everything dropped from kits, except mobs a player has taken over.
    /// </summary>
    public int Reset()
    {
        var deleted = 0;
        var query = EntityQueryEnumerator<TestChamberSpawnedComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (HasComp<ActorComponent>(uid))
                continue;

            QueueDel(uid);
            deleted++;
        }

        return deleted;
    }

    /// <summary>
    /// Floor tiles around the center that nothing stands on and that are visible from the center, closest first.
    /// </summary>
    private IEnumerable<Vector2i> FreeTiles(Entity<MapGridComponent> grid, Vector2i center, int range)
    {
        var origin = _transform.ToMapCoordinates(_map.GridTileToLocal(grid, grid, center));
        for (var r = 1; r <= range; r++)
        {
            var ring = new List<Vector2i>();
            for (var x = -r; x <= r; x++)
            {
                for (var y = -r; y <= r; y++)
                {
                    if (Math.Max(Math.Abs(x), Math.Abs(y)) == r)
                        ring.Add(center + new Vector2i(x, y));
                }
            }

            foreach (var indices in ring.OrderBy(t => (t - center).LengthSquared))
            {
                if (IsFree(grid, indices, origin))
                    yield return indices;
            }
        }
    }

    private bool IsFree(Entity<MapGridComponent> grid, Vector2i indices, MapCoordinates origin)
    {
        if (!_map.TryGetTileRef(grid, grid, indices, out var tileRef) || _turf.IsSpace(tileRef))
            return false;

        if (_turf.IsTileBlocked(grid, indices, BlockingMask, grid.Comp))
            return false;

        var anchored = _map.GetAnchoredEntitiesEnumerator(grid, grid, indices);
        while (anchored.MoveNext(out var uid))
        {
            if (HasComp<DoorComponent>(uid) || HasComp<TestChamberConsoleComponent>(uid))
                return false;
        }

        var target = _transform.ToMapCoordinates(_map.GridTileToLocal(grid, grid, indices));
        // Only walls and other anchored things block the view, not the player or the console standing in the middle
        return _interaction.InRangeUnobstructed(origin, target, range: 0f,
            predicate: uid => !Transform(uid).Anchored || HasComp<TestChamberConsoleComponent>(uid));
    }
}
