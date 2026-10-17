using System.Linq;
using Content.Shared._Polonium.Tutorial.Components;
using Robust.Client.Player;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Polonium.Tutorial;

/// <summary>
/// Drops the tile-point sprite on tiles a step asks for: where to stand, where a frame goes, which way a cable runs.
/// They are client-only entities with nothing to click, so they cannot be hovered, outlined, examined or bumped into,
/// and they go away by themselves once their tile is taken or the step moves on.
/// </summary>
public sealed partial class TutorialTileHighlightSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;

    private static readonly EntProtoId MarkerPrototype = "TutorialTileGlow";

    private readonly Dictionary<(EntityUid Grid, Vector2i Tile), EntityUid> _markers = new();

    // this runs every frame, so the working sets are kept around instead of rebuilt
    private readonly HashSet<(EntityUid Grid, Vector2i Tile)> _wanted = new();
    private readonly List<(EntityUid Grid, Vector2i Tile)> _dropped = new();

    public override void Shutdown()
    {
        ClearAll();
        base.Shutdown();
    }

    public override void FrameUpdate(float frameTime)
    {
        if (_player.LocalEntity is not { } player
            || !TryComp<TutorialSessionComponent>(player, out var session)
            || session.CurrentStep is not { } stepId
            || !_proto.TryIndex(stepId, out var step)
            || step.HighlightTiles.Count == 0
            || Transform(player).GridUid is not { } grid
            || !TryComp<MapGridComponent>(grid, out var gridComp))
        {
            ClearAll();
            return;
        }

        _wanted.Clear();
        foreach (var highlight in step.HighlightTiles)
        {
            if (FindAnchor(grid, highlight.Anchor) is not { } from)
                continue;

            var start = _map.TileIndicesFor(grid, gridComp, Transform(from).Coordinates);
            var end = highlight.To is { } toId && FindAnchor(grid, toId) is { } to
                ? _map.TileIndicesFor(grid, gridComp, Transform(to).Coordinates)
                : start;

            foreach (var tile in Route(start, end))
            {
                if (!IsTaken((grid, gridComp), tile, highlight.Until))
                    _wanted.Add((grid, tile));
            }
        }

        foreach (var key in _wanted)
        {
            if (_markers.TryGetValue(key, out var existing) && !Deleted(existing))
                continue;

            _markers[key] = Spawn(MarkerPrototype, _map.GridTileToLocal(grid, gridComp, key.Tile));
        }

        _dropped.Clear();
        foreach (var key in _markers.Keys)
        {
            if (!_wanted.Contains(key))
                _dropped.Add(key);
        }

        foreach (var key in _dropped)
            RemoveMarker(key);
    }

    /// <summary>Along one axis, then the other, so two anchors in a row give every tile between them.</summary>
    private static IEnumerable<Vector2i> Route(Vector2i start, Vector2i end)
    {
        var x = start.X;
        var stepX = Math.Sign(end.X - start.X);
        while (true)
        {
            yield return new Vector2i(x, start.Y);
            if (x == end.X)
                break;

            x += stepX;
        }

        var y = start.Y;
        var stepY = Math.Sign(end.Y - start.Y);
        while (y != end.Y)
        {
            y += stepY;
            yield return new Vector2i(end.X, y);
        }
    }

    private bool IsTaken(Entity<MapGridComponent> grid, Vector2i tile, List<EntProtoId> until)
    {
        if (until.Count == 0)
            return false;

        foreach (var uid in _lookup.GetEntitiesInRange(_map.GridTileToLocal(grid, grid.Comp, tile), 0.45f))
        {
            if (MetaData(uid).EntityPrototype is not { } proto || !until.Any(p => p.Id == proto.ID))
                continue;

            if (_map.TileIndicesFor(grid, grid.Comp, Transform(uid).Coordinates) == tile)
                return true;
        }

        return false;
    }

    private EntityUid? FindAnchor(EntityUid grid, string anchorId)
    {
        var query = EntityQueryEnumerator<TutorialAnchorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var anchor, out var xform))
        {
            if (anchor.AnchorId == anchorId && xform.GridUid == grid)
                return uid;
        }

        return null;
    }

    private void RemoveMarker((EntityUid Grid, Vector2i Tile) key)
    {
        if (_markers.Remove(key, out var marker) && !Deleted(marker))
            QueueDel(marker);
    }

    private void ClearAll()
    {
        if (_markers.Count == 0)
            return;

        _dropped.Clear();
        _dropped.AddRange(_markers.Keys);

        foreach (var key in _dropped)
            RemoveMarker(key);
    }
}
