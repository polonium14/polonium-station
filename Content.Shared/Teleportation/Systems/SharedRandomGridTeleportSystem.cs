using System.Numerics;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;

namespace Content.Shared.Teleportation.Systems;

/// <summary>
/// Finds random valid tiles on a grid and teleports entities there
/// </summary>
public sealed partial class SharedRandomGridTeleportSystem : EntitySystem
{
    public const float DefaultMinDistance = 8f;
    public const float DefaultMaxDistance = 10f;

    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;

    /// <summary>
    /// Finds a random unblocked tile on the same grid  in the requested distance range.
    /// The range is clamped when the grid is smaller than requested
    /// </summary>
    public bool TryFindRandomCoordinates(
        EntityCoordinates origin,
        out EntityCoordinates destination,
        float minDistance = DefaultMinDistance,
        float maxDistance = DefaultMaxDistance,
        CollisionGroup mask = CollisionGroup.MobMask,
        EntityUid? restrictGrid = null,
        int tries = 40)
    {
        destination = default;

        if (!origin.IsValid(EntityManager))
            return false;

        restrictGrid ??= _transform.GetGrid(origin);
        if (restrictGrid == null || !TryComp<MapGridComponent>(restrictGrid, out var grid))
            return false;

        var (effectiveMin, effectiveMax) = GetEffectiveDistanceRange(origin, restrictGrid.Value, grid, minDistance, maxDistance);
        if (effectiveMax <= 0)
            return false;

        for (var i = 0; i < tries; i++)
        {
            var distance = (effectiveMax - effectiveMin) * MathF.Sqrt(_random.NextFloat()) * (1 - (float) i / tries) + effectiveMin;
            var candidate = origin.Offset(_random.NextAngle().ToVec() * distance);

            if (_transform.GetGrid(candidate) != restrictGrid)
                continue;

            if (!_turf.TryGetTileRef(candidate, out var tileRef)
                || tileRef == null
                || _turf.IsSpace(tileRef.Value)
                || _turf.IsTileBlocked(tileRef.Value, mask))
                continue;

            destination = candidate.AlignWithClosestGridTile(entityManager: EntityManager);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Teleports an entity to a random valid location on the same grid as <paramref name="origin"/>.
    /// </summary>
    public bool TryTeleportEntity(
        EntityUid entity,
        EntityCoordinates origin,
        float minDistance = DefaultMinDistance,
        float maxDistance = DefaultMaxDistance,
        CollisionGroup? mask = null)
    {
        mask ??= TryComp<PhysicsComponent>(entity, out var physics)
            ? (CollisionGroup) physics.CollisionMask
            : CollisionGroup.MobMask;

        if (!TryFindRandomCoordinates(origin, out var destination, minDistance, maxDistance, mask.Value))
            return false;

        _transform.SetCoordinates(entity, destination);
        _transform.AttachToGridOrMap(entity);
        return true;
    }

    /// <summary>
    /// Returns the distance range that can actually be used from <paramref name="origin"/> on this grid.
    /// </summary>
    public (float Min, float Max) GetEffectiveDistanceRange(
        EntityCoordinates origin,
        EntityUid gridUid,
        MapGridComponent grid,
        float minDistance,
        float maxDistance)
    {
        var mapCoords = _transform.ToMapCoordinates(origin);
        var invMatrix = _transform.GetInvWorldMatrix(Transform(gridUid));
        var localPos = Vector2.Transform(mapCoords.Position, invMatrix);

        var aabb = grid.LocalAABB;
        var margin = grid.TileSize * 0.5f;

        var maxFromGrid = Math.Max(0,
            Math.Min(
                Math.Min(localPos.X - aabb.Left, aabb.Right - localPos.X),
                Math.Min(localPos.Y - aabb.Bottom, aabb.Top - localPos.Y)) - margin);

        var effectiveMax = Math.Min(maxDistance, maxFromGrid);
        var effectiveMin = Math.Min(minDistance, effectiveMax);

        return (effectiveMin, effectiveMax);
    }
}
