using System.Numerics;
using Content.Shared._Polonium.Atmos.AirFlow;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.CCVar;
using Content.Shared.Maps;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Atmos.AirFlow;

public sealed partial class AirFlowVisualsSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedAtmosphereSystem _atmos = default!;
    [Dependency] private SharedGasTileOverlaySystem _gasOverlay = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private TurfSystem _turf = default!;

    /// <summary>
    /// How long a tile keeps its flow without news from the server. The server says when a flow stops, so this
    /// only matters when its updates no longer arrive, for example after the player has left the area.
    /// It has to outlast an atmos cycle of a heavily loaded server.
    /// </summary>
    private static readonly TimeSpan HoldTime = TimeSpan.FromSeconds(5);
    private const float SmoothInterval = 0.1f;
    private const float FieldRelax = 5f;
    private const float MinStrength = 0.05f;

    /// <summary>
    /// Seconds in which the effect fades once the flow stops, for example when the breach is sealed.
    /// </summary>
    private const float FadeTime = 0.3f;

    private static readonly (Vector2i Offset, float Weight)[] Kernel =
    {
        (new Vector2i(0, 0), 4f),
        (new Vector2i(1, 0), 2f),
        (new Vector2i(-1, 0), 2f),
        (new Vector2i(0, 1), 2f),
        (new Vector2i(0, -1), 2f),
        (new Vector2i(1, 1), 1f),
        (new Vector2i(-1, 1), 1f),
        (new Vector2i(1, -1), 1f),
        (new Vector2i(-1, -1), 1f),
    };

    private static readonly Vector2i[] Neighbours = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

    private readonly Dictionary<EntityUid, GridFlow> _grids = new();
    private readonly List<EntityUid> _gridRemoveBuffer = new();
    private readonly List<Vector2i> _tileRemoveBuffer = new();
    private readonly Queue<Vector2i> _reachQueue = new();

    private float _smoothTimer;
    private int _activeCells;
    private bool _reducedMotion;
    private int _radius;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<AirFlowVisualsUpdateEvent>(OnFlowUpdate);
        Subs.CVar(_cfg, CCVars.ReducedMotion, value => _reducedMotion = value, true);
        Subs.CVar(_cfg, CCVars.AirFlowVisualsRadius, value => _radius = Math.Max(0, value), true);

        _overlay.AddOverlay(new AirFlowParticleOverlay(this));
        _overlay.AddOverlay(new AirFlowHazeOverlay(this));
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay<AirFlowParticleOverlay>();
        _overlay.RemoveOverlay<AirFlowHazeOverlay>();
    }

    /// <summary>
    /// Smoothed flow on a tile as a grid-local vector whose length is the strength, 0 to 1.
    /// </summary>
    public Vector2 GetFlow(EntityUid grid, Vector2i tile)
    {
        return _grids.TryGetValue(grid, out var flow) && flow.Cells.TryGetValue(tile, out var cell)
            ? cell.Value
            : Vector2.Zero;
    }

    private void OnFlowUpdate(AirFlowVisualsUpdateEvent ev)
    {
        var uid = GetEntity(ev.Grid);
        if (!_grids.TryGetValue(uid, out var flow))
        {
            if (ev.Chunks.Count == 0 || !TryComp(uid, out MapGridComponent? grid))
                return;

            _grids[uid] = flow = new GridFlow(uid, grid);
        }

        var now = _timing.CurTime;
        flow.Update++;

        foreach (var chunk in ev.Chunks)
        {
            if (chunk.Tiles.Length != AirFlowVisuals.ChunkSize * AirFlowVisuals.ChunkSize)
                continue;

            for (var i = 0; i < chunk.Tiles.Length; i++)
            {
                var value = chunk.Tiles[i];
                if (value == 0)
                    continue;

                var tile = AirFlowVisuals.GetTile(chunk.Index, i);
                if (!flow.Cells.TryGetValue(tile, out var cell))
                    flow.Cells[tile] = cell = new FlowCell();

                cell.Raw = AirFlowVisuals.Decode(value, out cell.Source);
                cell.LastSeen = now;
                cell.Update = flow.Update;
            }
        }

        // The update lists every flowing tile, so whatever it left out has stopped.
        foreach (var cell in flow.Cells.Values)
        {
            if (cell.Update == flow.Update)
                continue;

            cell.Raw = Vector2.Zero;
            cell.Source = false;
        }

        _smoothTimer = 0f;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_grids.Count == 0 && _count == 0)
            return;

        UpdateField(frameTime);
        SpawnParticles(frameTime);
        UpdateParticles(frameTime);
    }

    private void UpdateField(float frameTime)
    {
        _smoothTimer -= frameTime;
        var smooth = _smoothTimer <= 0f;
        if (smooth)
            _smoothTimer = SmoothInterval;

        var now = _timing.CurTime;
        var blend = 1f - MathF.Exp(-FieldRelax * frameTime);
        _activeCells = 0;

        foreach (var (uid, flow) in _grids)
        {
            if (Deleted(uid) || flow.Cells.Count == 0 && flow.Particles == 0)
            {
                _gridRemoveBuffer.Add(uid);
                continue;
            }

            if (smooth)
                Smooth(flow, now);

            flow.Fade = flow.Breached
                ? MathF.Min(1f, flow.Fade + frameTime / FadeTime)
                : MathF.Max(0f, flow.Fade - frameTime / FadeTime);

            foreach (var cell in flow.Cells.Values)
            {
                cell.Value += (cell.Target - cell.Value) * blend;
                if (cell.Reach > 0f && cell.Value.LengthSquared() >= MinStrength * MinStrength)
                    _activeCells++;
            }
        }

        foreach (var uid in _gridRemoveBuffer)
        {
            _grids.Remove(uid);
        }

        _gridRemoveBuffer.Clear();
    }

    /// <summary>
    /// The server reports one of four directions per tile. Averaging with the neighbours turns the staircase
    /// paths this gives into diagonal ones.
    /// </summary>
    private void Smooth(GridFlow flow, TimeSpan now)
    {
        flow.Breached = false;

        foreach (var (tile, cell) in flow.Cells)
        {
            if (now - cell.LastSeen > HoldTime)
            {
                cell.Raw = Vector2.Zero;
                cell.Source = false;
            }

            cell.Reach = 0f;
            if (!cell.Source)
                continue;

            cell.Reach = 1f;
            flow.Breached = true;
            _reachQueue.Enqueue(tile);
        }

        // The effect belongs to the breach, so it fades with the distance walked from it.
        var step = 1f / (_radius + 1);
        while (_reachQueue.TryDequeue(out var tile))
        {
            var reach = flow.Cells[tile].Reach - step;
            if (reach <= 0f)
                continue;

            foreach (var offset in Neighbours)
            {
                if (!flow.Cells.TryGetValue(tile + offset, out var other) || other.Reach >= reach)
                    continue;

                other.Reach = reach;
                _reachQueue.Enqueue(tile + offset);
            }
        }

        foreach (var (tile, cell) in flow.Cells)
        {
            var sum = Vector2.Zero;
            var weight = 0f;

            foreach (var (offset, w) in Kernel)
            {
                if (!flow.Cells.TryGetValue(tile + offset, out var other))
                    continue;

                sum += other.Raw * w;
                weight += w;
            }

            cell.Target = sum / weight;

            if (cell.Raw == Vector2.Zero && cell.Target == Vector2.Zero && cell.Value.LengthSquared() < 0.0001f)
                _tileRemoveBuffer.Add(tile);
        }

        foreach (var tile in _tileRemoveBuffer)
        {
            flow.Cells.Remove(tile);
        }

        _tileRemoveBuffer.Clear();
    }

    private Vector2 Sample(GridFlow flow, Vector2 pos)
    {
        var fx = pos.X - 0.5f;
        var fy = pos.Y - 0.5f;
        var x0 = (int) MathF.Floor(fx);
        var y0 = (int) MathF.Floor(fy);
        var tx = fx - x0;
        var ty = fy - y0;

        var result = Vector2.Zero;
        if (flow.Cells.TryGetValue(new Vector2i(x0, y0), out var cell))
            result += cell.Value * ((1f - tx) * (1f - ty));
        if (flow.Cells.TryGetValue(new Vector2i(x0 + 1, y0), out cell))
            result += cell.Value * (tx * (1f - ty));
        if (flow.Cells.TryGetValue(new Vector2i(x0, y0 + 1), out cell))
            result += cell.Value * ((1f - tx) * ty);
        if (flow.Cells.TryGetValue(new Vector2i(x0 + 1, y0 + 1), out cell))
            result += cell.Value * (tx * ty);

        return result;
    }

    private bool IsVoid(GridFlow flow, Vector2i tile)
    {
        return !_map.TryGetTileRef(flow.Uid, flow.Grid, tile, out var tileRef)
               || tileRef.Tile.IsEmpty
               || _turf.IsSpace(tileRef);
    }

    internal float GetFade(EntityUid grid)
    {
        return _grids.TryGetValue(grid, out var flow) ? flow.Fade : 0f;
    }

    private bool CanEnter(GridFlow flow, Vector2i tile)
    {
        return flow.Cells.ContainsKey(tile) || IsVoid(flow, tile);
    }

    private sealed class FlowCell
    {
        public Vector2 Raw;
        public Vector2 Target;
        public Vector2 Value;
        public TimeSpan LastSeen;
        public bool Source;
        public float Reach;
        public int Update;
    }

    private sealed class GridFlow(EntityUid uid, MapGridComponent grid)
    {
        public readonly EntityUid Uid = uid;
        public readonly MapGridComponent Grid = grid;
        public readonly Dictionary<Vector2i, FlowCell> Cells = new();
        public int Particles;
        public int Update;
        public bool Breached;
        public float Fade;
    }
}
