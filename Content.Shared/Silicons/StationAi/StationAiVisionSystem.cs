using Content.Shared.Power.EntitySystems;
using Content.Shared.StationAi;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Threading;

namespace Content.Shared.Silicons.StationAi;

public sealed partial class StationAiVisionSystem : EntitySystem
{
    /*
     * This class handles 2 things:
     * 1. It handles general "what tiles are visible" line of sight checks.
     * 2. It does single-tile lookups to tell if they're visible or not with support for a faster range-only path.
     *
     * IsAccessible is called from ActorRangeCheckJob (parallel BUI range checks), so all mutable
     * scratch state lives in a per-thread VisionScratch rather than on the system instance.
     */

    [Dependency] private IParallelManager _parallel = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private SharedTransformSystem _xforms = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;

    [Dependency] private EntityQuery<OccluderComponent> _occluderQuery = default!;

    [ThreadStatic] private static VisionScratch? _scratch;

    /// <summary>
    /// Returns whether a tile is accessible based on vision.
    /// </summary>
    public bool IsAccessible(Entity<BroadphaseComponent, MapGridComponent> grid, Vector2i tile, float expansionSize = 8.5f, bool fastPath = false)
    {
        var scratch = GetScratch();
        var viewportTiles = scratch.ViewportTiles;
        var opaque = scratch.Opaque;
        var seeds = scratch.Seeds;
        ref var seedJob = ref scratch.SeedJob;
        ref var job = ref scratch.Job;

        viewportTiles.Clear();
        opaque.Clear();
        seeds.Clear();
        viewportTiles.Add(tile);
        var localBounds = _lookup.GetLocalBounds(tile, grid.Comp2.TileSize);
        var expandedBounds = localBounds.Enlarged(expansionSize);

        seedJob.Grid = (grid.Owner, grid.Comp2);
        seedJob.ExpandedBounds = expandedBounds;
        _parallel.ProcessNow(seedJob);
        job.Data.Clear();
        job.FastPath = fastPath;

        foreach (var seed in seeds)
        {
            if (!seed.Comp.Enabled)
                continue;

            if (seed.Comp.NeedsPower && !_power.IsPowered(seed.Owner))
                continue;

            if (seed.Comp.NeedsAnchoring && !Transform(seed.Owner).Anchored)
                continue;

            job.Data.Add(seed);
        }

        if (seeds.Count == 0)
            return false;

        // Skip occluders step if we're just doing range checks.
        if (!fastPath)
        {
            var tileEnumerator = _maps.GetLocalTilesEnumerator(grid, grid, expandedBounds, ignoreEmpty: false);

            // Get all other relevant tiles.
            while (tileEnumerator.MoveNext(out var tileRef))
            {
                if (IsOccluded(grid, tileRef.GridIndices, scratch.Occluders))
                {
                    opaque.Add(tileRef.GridIndices);
                }
            }
        }

        for (var i = job.Vis1.Count; i < job.Data.Count; i++)
        {
            job.Vis1.Add(new Dictionary<Vector2i, int>());
            job.Vis2.Add(new Dictionary<Vector2i, int>());
            job.SeedTiles.Add(new HashSet<Vector2i>());
            job.BoundaryTiles.Add(new HashSet<Vector2i>());
        }

        scratch.SingleTiles.Clear();
        job.Grid = (grid.Owner, grid.Comp2);
        job.VisibleTiles = scratch.SingleTiles;
        _parallel.ProcessNow(job, job.Data.Count);

        return job.VisibleTiles.Contains(tile);
    }

    private bool IsOccluded(
        Entity<BroadphaseComponent, MapGridComponent> grid,
        Vector2i tile,
        HashSet<Entity<OccluderComponent>> occluders)
    {
        var tileBounds = _lookup.GetLocalBounds(tile, grid.Comp2.TileSize).Enlarged(-0.05f);
        occluders.Clear();
        _lookup.GetLocalEntitiesIntersecting((grid.Owner, grid.Comp1), tileBounds, occluders, query: _occluderQuery, flags: LookupFlags.Static | LookupFlags.Approximate);

        foreach (var occluder in occluders)
        {
            if (!occluder.Comp.Enabled)
                continue;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets a byond-equivalent for tiles in the specified worldAABB.
    /// </summary>
    /// <param name="expansionSize">How much to expand the bounds before to find vision intersecting it. Makes this the largest vision size + 1 tile.</param>
    public void GetView(Entity<BroadphaseComponent, MapGridComponent> grid, Box2Rotated worldBounds, HashSet<Vector2i> visibleTiles, float expansionSize = 8.5f)
    {
        var scratch = GetScratch();
        var viewportTiles = scratch.ViewportTiles;
        var opaque = scratch.Opaque;
        var seeds = scratch.Seeds;
        ref var seedJob = ref scratch.SeedJob;
        ref var job = ref scratch.Job;

        viewportTiles.Clear();
        opaque.Clear();
        seeds.Clear();

        // TODO: Would be nice to be able to run this while running the other stuff.
        seedJob.Grid = (grid.Owner, grid.Comp2);
        var invMatrix = _xforms.GetInvWorldMatrix(grid);
        var localAabb = invMatrix.TransformBox(worldBounds);
        var enlargedLocalAabb = invMatrix.TransformBox(worldBounds.Enlarged(expansionSize));
        seedJob.ExpandedBounds = enlargedLocalAabb;
        _parallel.ProcessNow(seedJob);
        job.Data.Clear();
        job.FastPath = false;

        foreach (var seed in seeds)
        {
            if (!seed.Comp.Enabled)
                continue;

            if (seed.Comp.NeedsPower && !_power.IsPowered(seed.Owner))
                continue;

            if (seed.Comp.NeedsAnchoring && !Transform(seed.Owner).Anchored)
                continue;

            job.Data.Add(seed);
        }

        if (seeds.Count == 0)
            return;

        // Get viewport tiles
        var tileEnumerator = _maps.GetLocalTilesEnumerator(grid, grid, localAabb, ignoreEmpty: false);

        while (tileEnumerator.MoveNext(out var tileRef))
        {
            if (IsOccluded(grid, tileRef.GridIndices, scratch.Occluders))
            {
                opaque.Add(tileRef.GridIndices);
            }

            viewportTiles.Add(tileRef.GridIndices);
        }

        tileEnumerator = _maps.GetLocalTilesEnumerator(grid, grid, enlargedLocalAabb, ignoreEmpty: false);

        while (tileEnumerator.MoveNext(out var tileRef))
        {
            if (viewportTiles.Contains(tileRef.GridIndices))
                continue;

            if (IsOccluded(grid, tileRef.GridIndices, scratch.Occluders))
            {
                opaque.Add(tileRef.GridIndices);
            }
        }

        // Wait for seed job here

        for (var i = job.Vis1.Count; i < job.Data.Count; i++)
        {
            job.Vis1.Add(new Dictionary<Vector2i, int>());
            job.Vis2.Add(new Dictionary<Vector2i, int>());
            job.SeedTiles.Add(new HashSet<Vector2i>());
            job.BoundaryTiles.Add(new HashSet<Vector2i>());
        }

        job.Grid = (grid.Owner, grid.Comp2);
        job.VisibleTiles = visibleTiles;
        _parallel.ProcessNow(job, job.Data.Count);
    }

    private VisionScratch GetScratch()
    {
        if (_scratch != null)
            return _scratch;

        var scratch = new VisionScratch();
        scratch.SeedJob = new SeedJob
        {
            System = this,
            Seeds = scratch.Seeds,
        };
        scratch.Job = new ViewJob
        {
            EntManager = EntityManager,
            Maps = _maps,
            System = this,
            Xforms = _xforms,
            VisibleTiles = scratch.SingleTiles,
            Opaque = scratch.Opaque,
            ViewportTiles = scratch.ViewportTiles,
        };
        return _scratch = scratch;
    }

    private int GetMaxDelta(Vector2i tile, Vector2i center)
    {
        var delta = tile - center;
        return Math.Max(Math.Abs(delta.X), Math.Abs(delta.Y));
    }

    private int GetSumDelta(Vector2i tile, Vector2i center)
    {
        var delta = tile - center;
        return Math.Abs(delta.X) + Math.Abs(delta.Y);
    }

    /// <summary>
    /// Checks if any of a tile's neighbors are visible.
    /// </summary>
    private bool CheckNeighborsVis(
        Dictionary<Vector2i, int> vis,
        Vector2i index,
        int d)
    {
        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0)
                    continue;

                var neighbor = index + new Vector2i(x, y);
                var neighborD = vis.GetValueOrDefault(neighbor);

                if (neighborD == d)
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Checks whether this tile fits the definition of a "corner"
    /// </summary>
    private bool IsCorner(
        HashSet<Vector2i> tiles,
        HashSet<Vector2i> blocked,
        Dictionary<Vector2i, int> vis1,
        Vector2i index,
        Vector2i delta)
    {
        var diagonalIndex = index + delta;

        if (!tiles.TryGetValue(diagonalIndex, out var diagonal))
            return false;

        var cardinal1 = new Vector2i(index.X, diagonal.Y);
        var cardinal2 = new Vector2i(diagonal.X, index.Y);

        return vis1.GetValueOrDefault(diagonal) != 0 &&
               vis1.GetValueOrDefault(cardinal1) != 0 &&
               vis1.GetValueOrDefault(cardinal2) != 0 &&
               blocked.Contains(cardinal1) &&
               blocked.Contains(cardinal2) &&
               !blocked.Contains(diagonal);
    }

    private sealed class VisionScratch
    {
        public readonly HashSet<Entity<OccluderComponent>> Occluders = new();
        public readonly HashSet<Entity<StationAiVisionComponent>> Seeds = new();
        public readonly HashSet<Vector2i> ViewportTiles = new();
        public readonly HashSet<Vector2i> Opaque = new();
        public readonly HashSet<Vector2i> SingleTiles = new();

        public SeedJob SeedJob;
        public ViewJob Job;
    }

    /// <summary>
    /// Gets the relevant vision seeds for later.
    /// </summary>
    private record struct SeedJob() : IRobustJob
    {
        public required StationAiVisionSystem System;
        public required HashSet<Entity<StationAiVisionComponent>> Seeds;

        public Entity<MapGridComponent> Grid;
        public Box2 ExpandedBounds;

        public void Execute()
        {
            System._lookup.GetLocalEntitiesIntersecting(Grid.Owner, ExpandedBounds, Seeds, flags: LookupFlags.All | LookupFlags.Approximate);
        }
    }

    private record struct ViewJob() : IParallelRobustJob
    {
        public int BatchSize => 1;

        public required IEntityManager EntManager;
        public required SharedMapSystem Maps;
        public required StationAiVisionSystem System;
        public required SharedTransformSystem Xforms;

        public Entity<MapGridComponent> Grid;
        public List<Entity<StationAiVisionComponent>> Data = new();

        public required HashSet<Vector2i> VisibleTiles;
        public required HashSet<Vector2i> Opaque;
        public required HashSet<Vector2i> ViewportTiles;

        /// <summary>
        /// Do we skip line of sight checks and just check vision ranges.
        /// </summary>
        public bool FastPath;

        public readonly List<Dictionary<Vector2i, int>> Vis1 = new();
        public readonly List<Dictionary<Vector2i, int>> Vis2 = new();

        public readonly List<HashSet<Vector2i>> SeedTiles = new();
        public readonly List<HashSet<Vector2i>> BoundaryTiles = new();

        public void Execute(int index)
        {
            var seed = Data[index];
            var seedXform = EntManager.GetComponent<TransformComponent>(seed);

            // Fastpath just get tiles in range.
            // Either xray-vision or system is doing a quick-and-dirty check.
            if (!seed.Comp.Occluded || FastPath)
            {
                var squircles = Maps.GetLocalTilesIntersecting(Grid.Owner,
                    Grid.Comp,
                    new Circle(Xforms.GetWorldPosition(seedXform), seed.Comp.Range), ignoreEmpty: false);

                lock (VisibleTiles)
                {
                    foreach (var tile in squircles)
                    {
                        VisibleTiles.Add(tile.GridIndices);
                    }
                }

                return;
            }

            // Code based upon https://github.com/OpenDreamProject/OpenDream/blob/c4a3828ccb997bf3722673620460ebb11b95ccdf/OpenDreamShared/Dream/ViewAlgorithm.cs

            var range = seed.Comp.Range;
            var vis1 = Vis1[index];
            var vis2 = Vis2[index];

            var seedTiles = SeedTiles[index];
            var boundary = BoundaryTiles[index];

            // Cleanup last run
            vis1.Clear();
            vis2.Clear();

            seedTiles.Clear();
            boundary.Clear();

            var maxDepthMax = 0;
            var sumDepthMax = 0;

            var eyePos = Maps.GetTileRef(Grid.Owner, Grid, seedXform.Coordinates).GridIndices;

            for (var x = Math.Floor(eyePos.X - range); x <= eyePos.X + range; x++)
            {
                for (var y = Math.Floor(eyePos.Y - range); y <= eyePos.Y + range; y++)
                {
                    var tile = new Vector2i((int)x, (int)y);
                    var delta = tile - eyePos;
                    var xDelta = Math.Abs(delta.X);
                    var yDelta = Math.Abs(delta.Y);

                    var deltaSum = xDelta + yDelta;

                    maxDepthMax = Math.Max(maxDepthMax, Math.Max(xDelta, yDelta));
                    sumDepthMax = Math.Max(sumDepthMax, deltaSum);
                    seedTiles.Add(tile);
                }
            }

            // Step 3, Diagonal shadow loop
            for (var d = 0; d < maxDepthMax; d++)
            {
                foreach (var tile in seedTiles)
                {
                    var maxDelta = System.GetMaxDelta(tile, eyePos);

                    if (maxDelta == d + 1 && System.CheckNeighborsVis(vis2, tile, d))
                    {
                        vis2[tile] = (Opaque.Contains(tile) ? -1 : d + 1);
                    }
                }
            }

            // Step 4, Straight shadow loop
            for (var d = 0; d < sumDepthMax; d++)
            {
                foreach (var tile in seedTiles)
                {
                    var sumDelta = System.GetSumDelta(tile, eyePos);

                    if (sumDelta == d + 1 && System.CheckNeighborsVis(vis1, tile, d))
                    {
                        if (Opaque.Contains(tile))
                        {
                            vis1[tile] = -1;
                        }
                        else if (vis2.GetValueOrDefault(tile) != 0)
                        {
                            vis1[tile] = d + 1;
                        }
                    }
                }
            }

            // Add the eye itself
            vis1[eyePos] = 1;

            // Step 6.

            // Step 7.

            // Step 8.
            foreach (var tile in seedTiles)
            {
                vis2[tile] = vis1.GetValueOrDefault(tile, 0);
            }

            // Step 9
            foreach (var tile in seedTiles)
            {
                if (!Opaque.Contains(tile))
                    continue;

                var tileVis1 = vis1.GetValueOrDefault(tile);

                if (tileVis1 != 0)
                    continue;

                if (System.IsCorner(seedTiles, Opaque, vis1, tile, Vector2i.UpRight) ||
                    System.IsCorner(seedTiles, Opaque, vis1, tile, Vector2i.UpLeft) ||
                    System.IsCorner(seedTiles, Opaque, vis1, tile, Vector2i.DownLeft) ||
                    System.IsCorner(seedTiles, Opaque, vis1, tile, Vector2i.DownRight))
                {
                    boundary.Add(tile);
                }
            }

            // Make all wall/corner tiles visible
            foreach (var tile in boundary)
            {
                vis1[tile] = -1;
            }

            // vis2 is what we care about for LOS.
            foreach (var tile in seedTiles)
            {
                // If not in viewport don't care.
                if (!ViewportTiles.Contains(tile))
                    continue;

                var tileVis = vis1.GetValueOrDefault(tile, 0);

                if (tileVis != 0)
                {
                    // No idea if it's better to do this inside or out.
                    lock (VisibleTiles)
                    {
                        VisibleTiles.Add(tile);
                    }
                }
            }
        }
    }
}
