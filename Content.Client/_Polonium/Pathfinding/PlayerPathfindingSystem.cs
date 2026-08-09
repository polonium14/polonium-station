using System.Collections.Generic;
using System.Numerics;
using Content.Client.Clickable;
using Content.Client.Viewport;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared.Doors.Components;
using Content.Shared.Tiles;
using Content.Shared.Tag;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Content.Shared.Physics;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Polonium.Pathfinding;

public sealed class PlayerPathfindingSystem : EntitySystem
{
    private const float WaypointAdvanceThreshold = 0.15f;
    private const float TargetOutlineWidth = 1f;

    [ValidatePrototypeId<ShaderPrototype>]
    private const string TargetOutlineShader = "SelectionOutlineInrange";

    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;

    [ViewVariables(VVAccess.ReadWrite)]
    private float _timer = 0f;
    private const float UpdateRate = 0.75f;

    /// <summary>
    /// Cached start tile for pathfinding calculations.
    /// </summary>
    private Vector2i? _stableStartTile;

    private ShaderInstance? _targetOutlineShader;
    private ShaderInstance? _destinationOutlineShader;
    private EntityUid? _outlinedDestination;
    private SpriteComponent? _outlinedSprite;

    private static readonly ProtoId<TagPrototype> WallTag = "Wall";
    private static readonly ProtoId<TagPrototype> AirlockTag = "Airlock";

    public override void Initialize()
    {
        base.Initialize();

        _targetOutlineShader = _prototypeManager.Index<ShaderPrototype>(TargetOutlineShader).InstanceUnique();

        if (!_overlayManager.HasOverlay<PathfindingOverlay>())
            _overlayManager.AddOverlay(new PathfindingOverlay(this, EntityManager, _mapSystem, _playerManager, _transform));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        Reset();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var localPlayer = _playerManager.LocalEntity;
        if (localPlayer == null || !TryComp<PlayerPathfindingComponent>(localPlayer.Value, out var pathComp))
        {
            ClearDestinationOutline();
            return;
        }

        TryResolveAnchorDestination(localPlayer.Value, pathComp);
        UpdateDestinationOutline(pathComp);

        if (!pathComp.Active || pathComp.Destination == null || !Exists(pathComp.Destination.Value))
        {
            ClearPath(pathComp);
            return;
        }

        var playerXform = Transform(localPlayer.Value);
        if (playerXform.GridUid == null || !TryComp<MapGridComponent>(playerXform.GridUid.Value, out var grid))
            return;

        var localPos = _transform.GetRelativePosition(playerXform, playerXform.GridUid.Value);
        TrimPassedWaypoints(pathComp, localPos, grid.TileSize);

        _timer += frameTime;
        if (_timer < UpdateRate)
            return;

        _timer = 0f;
        CalculatePath(localPlayer.Value, pathComp.Destination.Value, pathComp);
    }

    /// <summary>
    /// Returns the index of the first path tile that should still be rendered.
    /// Tiles before this index lie behind the player along the route.
    /// </summary>
    public static int GetPathProgressIndex(IReadOnlyList<Vector2i> path, Vector2 localPlayerPos, float tileSize)
    {
        if (path.Count <= 1)
            return 0;

        var progress = 0;
        for (var i = 0; i < path.Count - 1; i++)
        {
            var segmentStart = TileCenter(path[i], tileSize);
            var segmentEnd = TileCenter(path[i + 1], tileSize);
            var segment = segmentEnd - segmentStart;
            var lenSq = segment.LengthSquared();

            if (lenSq < 0.001f)
                continue;

            var projection = Vector2.Dot(localPlayerPos - segmentStart, segment) / lenSq;
            if (projection >= 1f + WaypointAdvanceThreshold)
                progress = i + 1;
            else
                break;
        }

        return progress;
    }

    public static Vector2 TileCenter(Vector2i tile, float tileSize)
    {
        return new Vector2(tile.X * tileSize + tileSize / 2f, tile.Y * tileSize + tileSize / 2f);
    }

    public void SetDestinationAnchor(EntityUid player, string? anchorId)
    {
        var pathComp = EnsureComp<PlayerPathfindingComponent>(player);
        pathComp.DestinationAnchorId = anchorId;
        pathComp.Destination = null;
        pathComp.CurrentPath.Clear();
        pathComp.Active = !string.IsNullOrWhiteSpace(anchorId);

        if (pathComp.Active)
            TryResolveAnchorDestination(player, pathComp);
    }

    private void TryResolveAnchorDestination(EntityUid player, PlayerPathfindingComponent pathComp)
    {
        if (string.IsNullOrWhiteSpace(pathComp.DestinationAnchorId))
            return;

        if (!TryComp(player, out TransformComponent? playerXform) || playerXform.GridUid is not { } grid)
            return;

        // Find the closest anchor.
        // Prefer anchors that are on the same GridUid as the player, but also allow GridUid=null anchors
        // (some tutorial markers are placed in the map editor without an explicit grid assignment).
        var anchorId = pathComp.DestinationAnchorId;
        var playerPos = _transform.GetWorldPosition(playerXform);

        EntityUid? bestOnGridUid = null;
        var bestOnGridDistSq = float.MaxValue;

        EntityUid? bestNullGridUid = null;
        var bestNullGridDistSq = float.MaxValue;

        var query = EntityQueryEnumerator<TutorialAnchorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var anchor, out var anchorXform))
        {
            if (anchor.AnchorId != anchorId)
                continue;

            var distSq = (_transform.GetWorldPosition(anchorXform) - playerPos).LengthSquared();

            if (anchorXform.GridUid == grid)
            {
                if (distSq < bestOnGridDistSq)
                {
                    bestOnGridDistSq = distSq;
                    bestOnGridUid = uid;
                }
            }
            else if (anchorXform.GridUid is null)
            {
                // Fallback: keep something resolvable even when editor-placed anchors have no GridUid.
                if (distSq < bestNullGridDistSq)
                {
                    bestNullGridDistSq = distSq;
                    bestNullGridUid = uid;
                }
            }
        }

        pathComp.Destination = bestOnGridUid ?? bestNullGridUid;
    }

    private void CalculatePath(EntityUid player, EntityUid target, PlayerPathfindingComponent component)
    {
        var playerXform = Transform(player);
        var targetXform = Transform(target);

        if (playerXform.GridUid == null)
            return;

        var gridUid = playerXform.GridUid.Value;

        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        // Most targets are on the same grid as the player.
        // However, editor markers (and other special entities) can end up without a GridUid even though they are visible.
        // In that case, we still want to pathfind by projecting their map coordinates onto the player's grid.
        Vector2i targetTile;
        if (targetXform.GridUid != null)
        {
            if (targetXform.GridUid != gridUid)
                return;

            targetTile = _mapSystem.TileIndicesFor(gridUid, grid, targetXform.Coordinates);
        }
        else
        {
            var targetMap = _transform.ToMapCoordinates(targetXform.Coordinates);
            // Ensure target is on the same map as the player's grid.
            if (targetMap.MapId != Transform(gridUid).MapID)
                return;

            targetTile = _mapSystem.TileIndicesFor(gridUid, grid, targetMap);
        }

        var localPos = _transform.GetRelativePosition(playerXform, playerXform.GridUid.Value);
        var startTile = GetStableStartTile(localPos, grid.TileSize);

        var newPath = FindPath(gridUid, grid, startTile, targetTile);
        if (newPath.Count > 0)
            component.CurrentPath = newPath;
    }

    private Vector2i GetStableStartTile(Vector2 localPos, float tileSize)
    {
        var rawTile = new Vector2i(
            (int) System.Math.Floor(localPos.X / tileSize),
            (int) System.Math.Floor(localPos.Y / tileSize));

        if (_stableStartTile is not { } stable || stable == rawTile)
        {
            _stableStartTile = rawTile;
            return rawTile;
        }

        var stableCenter = TileCenter(stable, tileSize);
        var rawCenter = TileCenter(rawTile, tileSize);
        var distToStable = (localPos - stableCenter).LengthSquared();
        var distToRaw = (localPos - rawCenter).LengthSquared();
        var hysteresis = tileSize * tileSize * 0.04f;

        if (distToRaw + hysteresis < distToStable)
            _stableStartTile = rawTile;

        return _stableStartTile.Value;
    }

    private void TrimPassedWaypoints(PlayerPathfindingComponent component, Vector2 localPlayerPos, float tileSize)
    {
        if (component.CurrentPath.Count <= 1)
            return;

        var progress = GetPathProgressIndex(component.CurrentPath, localPlayerPos, tileSize);
        if (progress <= 0)
            return;

        component.CurrentPath.RemoveRange(0, progress);
        _stableStartTile = component.CurrentPath[0];
    }

    private void ClearPath(PlayerPathfindingComponent component)
    {
        component.CurrentPath.Clear();
        _stableStartTile = null;
    }

    private List<Vector2i> FindPath(EntityUid gridUid, MapGridComponent grid, Vector2i start, Vector2i target)
    {
        var openSet = new List<Vector2i> { start };
        var cameFrom = new Dictionary<Vector2i, Vector2i>();

        var gScore = new Dictionary<Vector2i, float> { [start] = 0 };
        var fScore = new Dictionary<Vector2i, float> { [start] = Heuristic(start, target) };

        int maxIterations = 2500;
        int iterations = 0;

        var directions = new[]
        {
            new Vector2i(0, 1),
            new Vector2i(1, 0),
            new Vector2i(0, -1),
            new Vector2i(-1, 0)
        };

        while (openSet.Count > 0 && iterations < maxIterations)
        {
            iterations++;

            var currentIdx = 0;
            var lowestF = fScore.GetValueOrDefault(openSet[0], float.MaxValue);
            for (int i = 1; i < openSet.Count; i++)
            {
                var cost = fScore.GetValueOrDefault(openSet[i], float.MaxValue);
                if (cost < lowestF)
                {
                    lowestF = cost;
                    currentIdx = i;
                }
            }

            var curr = openSet[currentIdx];
            openSet.RemoveAt(currentIdx);

            if (curr == target)
                return ReconstructPath(cameFrom, curr);

            foreach (var dir in directions)
            {
                var neighbor = curr + dir;

                if (!IsTilePassable(gridUid, grid, neighbor) && neighbor != target)
                    continue;

                float tentativeG = gScore[curr] + 1;

                if (!gScore.TryGetValue(neighbor, out var currentGScore) || tentativeG < currentGScore)
                {
                    cameFrom[neighbor] = curr;
                    gScore[neighbor] = tentativeG;

                    var newFScore = tentativeG + Heuristic(neighbor, target);
                    fScore[neighbor] = newFScore;

                    if (!openSet.Contains(neighbor))
                        openSet.Add(neighbor);
                }
            }
        }

        return new List<Vector2i>();
    }

    private float Heuristic(Vector2i a, Vector2i b)
    {
        return System.Math.Abs(b.X - a.X) + System.Math.Abs(b.Y - a.Y);
    }

    private List<Vector2i> ReconstructPath(Dictionary<Vector2i, Vector2i> cameFrom, Vector2i current)
    {
        var path = new List<Vector2i> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Insert(0, current);
        }
        return path;
    }

    private bool IsTilePassable(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        var tileRef = _mapSystem.GetTileRef(gridUid, grid, tile);
        if (tileRef.Tile.IsEmpty)
            return false;

        var anchored = _mapSystem.GetAnchoredEntities(gridUid, grid, tile);
        foreach (var ent in anchored)
        {
            if (HasComp<DoorComponent>(ent))
                continue;

            if (TryComp<PhysicsComponent>(ent, out var physics))
            {
                if ((physics.CollisionLayer & (int) CollisionGroup.Impassable) != 0)
                    return false;
            }
        }

        return true;
    }

    private void Reset()
    {
        _stableStartTile = null;
        ClearDestinationOutline();

        if (_overlayManager.HasOverlay<PathfindingOverlay>())
            _overlayManager.RemoveOverlay<PathfindingOverlay>();
    }

    /// <summary>
    /// Highlights interactable items
    /// </summary>
    private void UpdateDestinationOutline(PlayerPathfindingComponent pathComp)
    {
        if (!pathComp.Active
            || pathComp.Destination is not { } destination
            || !Exists(destination)
            || !IsPathfindingItemTarget(destination))
        {
            ClearDestinationOutline();
            return;
        }

        if (_outlinedDestination == destination)
            return;

        ClearDestinationOutline();
        ApplyDestinationOutline(destination);
    }

    private bool IsPathfindingItemTarget(EntityUid uid)
    {
        if (!TryComp(uid, out SpriteComponent? sprite) || !sprite.Visible)
            return false;

        if (HasComp<FloorTileComponent>(uid))
            return false;

        if (_tagSystem.HasTag(uid, WallTag))
            return false;

        if (_tagSystem.HasTag(uid, AirlockTag))
            return false;

        if (!HasComp<ClickableComponent>(uid))
            return false;

        return true;
    }

    private void ApplyDestinationOutline(EntityUid uid)
    {
        if (!TryComp(uid, out SpriteComponent? sprite) || _targetOutlineShader == null)
            return;

        if (sprite.PostShader != null && sprite.PostShader != _destinationOutlineShader)
            return;

        var renderScale = (int) _eyeManager.MainViewport.GetRenderScale();
        _destinationOutlineShader?.Dispose();
        _destinationOutlineShader = _targetOutlineShader.Duplicate();
        _destinationOutlineShader.SetParameter("outline_width", TargetOutlineWidth * renderScale);

        sprite.PostShader = _destinationOutlineShader;
        sprite.RenderOrder = EntityManager.CurrentTick.Value;
        _outlinedDestination = uid;
        _outlinedSprite = sprite;
    }

    private void ClearDestinationOutline()
    {
        if (_outlinedSprite != null)
        {
            if (_outlinedSprite.PostShader == _destinationOutlineShader)
            {
                _outlinedSprite.PostShader = null;
                _outlinedSprite.RenderOrder = 0;
            }

            _outlinedSprite = null;
        }

        _outlinedDestination = null;
        _destinationOutlineShader?.Dispose();
        _destinationOutlineShader = null;
    }
}
