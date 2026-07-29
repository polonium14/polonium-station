using System.Numerics;
using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared.Coordinates;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._RMC14.Xenonids.Weeds;

public sealed partial class XenoWeedsSystem : SharedXenoWeedsSystem
{
    [Dependency] private SharedXenoHiveSystem _hive = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private RMCMapSystem _rmcMap = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private EntityQuery<AllowWeedSpreadComponent> _allowSpreadQuery;
    private EntityQuery<BlockWeedsComponent> _blockQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<XenoWeedsComponent> _weedsQuery;

    // dont spawn while enumerating weeds comps - that mutates the query dict
    private readonly List<(EntityUid Uid, XenoWeedsComponent Weeds)> _spread = new();

    public override void Initialize()
    {
        base.Initialize();

        _allowSpreadQuery = GetEntityQuery<AllowWeedSpreadComponent>();
        _blockQuery = GetEntityQuery<BlockWeedsComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _weedsQuery = GetEntityQuery<XenoWeedsComponent>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _spread.Clear();

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<XenoWeedsSpreadingComponent, XenoWeedsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var spreading, out var weeds, out _))
        {
            if (!weeds.Spreads)
                continue;

            if (time < spreading.NextSpread)
                continue;

            spreading.NextSpread = time + spreading.SpreadDelay;
            Dirty(uid, spreading);
            _spread.Add((uid, weeds));
        }

        foreach (var (uid, weeds) in _spread)
        {
            if (_transform.GetGrid(uid) is not { } gridId ||
                !_gridQuery.TryComp(gridId, out var gridComp))
                continue;

            var grid = new Entity<MapGridComponent>(gridId, gridComp);
            var indices = _map.CoordinatesToTile(gridId, gridComp, uid.ToCoordinates());
            var source = weeds.IsSource ? uid : weeds.Source;
            if (source == null || !Exists(source) || !_weedsQuery.TryComp(source, out _))
                continue;

            var sourceLocal = _map.CoordinatesToTile(grid, gridComp, Transform(source.Value).Coordinates);

            foreach (var direction in _rmcMap.CardinalDirections)
            {
                var neighbor = indices.Offset(direction);
                var diff = Vector2.Abs(neighbor - sourceLocal);
                if (diff.X >= weeds.Range || diff.Y >= weeds.Range)
                    continue;

                var coords = _map.GridTileToLocal(grid, grid, neighbor);
                if (!CanSpreadOntoTile(grid, neighbor, coords))
                    continue;

                var spawned = Spawn(weeds.Spawns, coords);
                if (TryComp(spawned, out XenoWeedsComponent? childWeeds))
                {
                    childWeeds.IsSource = false;
                    childWeeds.Source = source;
                    childWeeds.Spreads = true;
                    Dirty(spawned, childWeeds);
                }

                _hive.SetSameHive(uid, spawned);
            }
        }
    }

    private bool CanSpreadOntoTile(Entity<MapGridComponent> grid, Vector2i indices, EntityCoordinates coords)
    {
        if (!_map.TryGetTileRef(grid, grid, indices, out var tile) || tile.Tile.IsEmpty)
            return false;

        if (_rmcMap.IsTileBlocked(coords))
            return false;

        var anchored = _rmcMap.GetAnchoredEntitiesEnumerator(grid, indices);
        while (anchored.MoveNext(out var uid))
        {
            if (_weedsQuery.HasComp(uid))
                return false;

            if (_blockQuery.HasComp(uid) && !_allowSpreadQuery.HasComp(uid))
                return false;
        }

        return true;
    }
}
