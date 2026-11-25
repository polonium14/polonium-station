using Content.Shared._Polonium.Atmos.AirFlow;
using Content.Shared.Atmos;
using Content.Shared.CCVar;
using Content.Shared.Chunking;
using Content.Shared.GameTicking;
using Microsoft.Extensions.ObjectPool;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Player;

namespace Content.Server._Polonium.Atmos.AirFlow;

public sealed partial class AirFlowVisualsSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private ChunkingSystem _chunking = default!;

    private static readonly Vector2i[] Neighbours = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

    private readonly Dictionary<EntityUid, Dictionary<Vector2i, Flow>> _pending = new();
    private readonly HashSet<EntityUid> _flowing = new();
    private readonly Queue<(Vector2i Tile, int Distance)> _queue = new();
    private readonly HashSet<Vector2i> _visited = new();

    private readonly ObjectPool<HashSet<Vector2i>> _indexPool =
        new DefaultObjectPool<HashSet<Vector2i>>(new DefaultPooledObjectPolicy<HashSet<Vector2i>>(), 64);

    private readonly ObjectPool<Dictionary<NetEntity, HashSet<Vector2i>>> _viewerPool =
        new DefaultObjectPool<Dictionary<NetEntity, HashSet<Vector2i>>>(
            new DefaultPooledObjectPolicy<Dictionary<NetEntity, HashSet<Vector2i>>>(), 64);

    private bool _enabled;
    private int _radius;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, CCVars.AirFlowVisualsEnabled, OnEnabledChanged, true);
        Subs.CVar(_cfg, CCVars.AirFlowVisualsRadius, value => _radius = Math.Max(0, value), true);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _pending.Clear();
        _flowing.Clear();
    }

    private void OnEnabledChanged(bool value)
    {
        _enabled = value;

        if (value)
            return;

        _pending.Clear();
        _flowing.Clear();
    }

    /// <summary>
    /// Records that air is rushing over a tile during the current atmos cycle of its grid.
    /// </summary>
    public void ReportTile(EntityUid grid, Vector2i tile, AtmosDirection direction, float difference)
    {
        if (!_enabled || difference < AirFlowVisuals.MinDifference || direction == AtmosDirection.Invalid)
            return;

        if (!_pending.TryGetValue(grid, out var tiles))
            _pending[grid] = tiles = new Dictionary<Vector2i, Flow>();

        if (!tiles.TryGetValue(tile, out var old) || difference >= old.Difference)
            tiles[tile] = new Flow(direction, difference);
    }

    /// <summary>
    /// Sends what was reported for the grid during the atmos cycle that has just ended. A grid whose flow
    /// has stopped gets one empty update, so clients drop the effect at once instead of waiting it out.
    /// </summary>
    public void EndCycle(EntityUid grid)
    {
        if (!_enabled)
            return;

        _pending.Remove(grid, out var tiles);
        if (tiles == null && !_flowing.Contains(grid))
            return;

        var chunks = tiles == null ? null : BuildChunks(tiles);
        if (chunks == null || chunks.Count == 0)
        {
            if (!_flowing.Remove(grid))
                return;

            chunks = null;
        }
        else
        {
            _flowing.Add(grid);
        }

        var netGrid = GetNetEntity(grid);
        foreach (var session in _player.Sessions)
        {
            if (session.Status == SessionStatus.InGame)
                SendTo(session, netGrid, chunks);
        }
    }

    /// <summary>
    /// Keeps only the tiles within reach of a place the flow converges on: a breach, a hole in the floor
    /// or a doorway. Such a place carries more air than any tile around it.
    /// </summary>
    private Dictionary<Vector2i, byte[]> BuildChunks(Dictionary<Vector2i, Flow> tiles)
    {
        var chunks = new Dictionary<Vector2i, byte[]>();
        _queue.Clear();
        _visited.Clear();

        foreach (var (tile, flow) in tiles)
        {
            if (flow.Difference < AirFlowVisuals.MinSourceDifference)
                continue;

            var source = true;
            foreach (var offset in Neighbours)
            {
                if (!tiles.TryGetValue(tile + offset, out var other) || other.Difference <= flow.Difference)
                    continue;

                source = false;
                break;
            }

            if (!source)
                continue;

            _visited.Add(tile);
            _queue.Enqueue((tile, 0));
            Write(chunks, tile, flow, true);
        }

        while (_queue.TryDequeue(out var entry))
        {
            if (entry.Distance >= _radius)
                continue;

            foreach (var offset in Neighbours)
            {
                var next = entry.Tile + offset;
                if (!tiles.TryGetValue(next, out var flow) || !_visited.Add(next))
                    continue;

                _queue.Enqueue((next, entry.Distance + 1));
                Write(chunks, next, flow, false);
            }
        }

        return chunks;
    }

    private static void Write(Dictionary<Vector2i, byte[]> chunks, Vector2i tile, Flow flow, bool source)
    {
        var chunk = AirFlowVisuals.GetChunkIndices(tile);
        if (!chunks.TryGetValue(chunk, out var data))
            chunks[chunk] = data = new byte[AirFlowVisuals.ChunkSize * AirFlowVisuals.ChunkSize];

        data[AirFlowVisuals.GetTileIndex(tile, chunk)] = AirFlowVisuals.Encode(flow.Direction, flow.Difference, source);
    }

    private void SendTo(ICommonSession session, NetEntity netGrid, Dictionary<Vector2i, byte[]>? chunks)
    {
        var visible = _chunking.GetChunksForSession(session, AirFlowVisuals.ChunkSize, _indexPool, _viewerPool);
        AirFlowVisualsUpdateEvent? ev = null;

        foreach (var (visibleGrid, indices) in visible)
        {
            if (visibleGrid == netGrid)
            {
                ev = new AirFlowVisualsUpdateEvent { Grid = netGrid };

                if (chunks != null)
                {
                    foreach (var index in indices)
                    {
                        if (chunks.TryGetValue(index, out var tiles))
                            ev.Chunks.Add(new AirFlowChunk(index, tiles));
                    }
                }
            }

            indices.Clear();
            _indexPool.Return(indices);
        }

        visible.Clear();
        _viewerPool.Return(visible);

        if (ev != null)
            RaiseNetworkEvent(ev, session.Channel);
    }

    private readonly record struct Flow(AtmosDirection Direction, float Difference);
}
