using System.Numerics;
using Content.Shared._Polonium.Atmos.AirFlow;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using Robust.Shared.Random;

namespace Content.Client._Polonium.Atmos.AirFlow;

public sealed partial class AirFlowVisualsSystem
{
    private const int MaxParticles = 1500;

    /// <summary>
    /// Spawn rates are per tile up to this many tiles with flow. Past it they thin out, so a depressurizing
    /// hall costs about as much as a room.
    /// </summary>
    private const float DenseCells = 160f;

    /// <summary>
    /// Speed in tiles per second of a particle carried by a flow of full strength.
    /// </summary>
    private const float FlowSpeed = 9.5f;

    private const float FogRate = 2.6f;
    private const float DustRate = 3.6f;
    private const float GrainRate = 0.35f;
    private const float GrainMinStrength = 0.3f;
    private const float WispRate = 3.2f;
    private const float HazeRate = 2.2f;

    private const float GasFogAlpha = 1.5f;

    /// <summary>
    /// How far gas streaks are pushed towards white, so they stand out against the overlay of the gas itself.
    /// </summary>
    private const float WispBrightening = 0.5f;

    /// <summary>
    /// How many times faster a particle ages once it is left outside the reach of every breach.
    /// </summary>
    private const float StrandedAging = 7f;

    private const float OutDrag = 1.3f;
    private const float OutSpread = 0.32f;

    private static readonly Color FogColor = Color.FromHex("#e9f4ff");

    private readonly Particle[] _particles = new Particle[MaxParticles];
    private readonly Dictionary<int, Color> _gasTints = new();
    private int _count;

    internal Particle[] Particles => _particles;
    internal int ParticleCount => _count;
    internal int HazeCount { get; private set; }

    private void SpawnParticles(float frameTime)
    {
        if (_activeCells == 0)
            return;

        var scale = MathF.Min(1f, DenseCells / _activeCells) * frameTime;

        foreach (var flow in _grids.Values)
        {
            foreach (var (tile, cell) in flow.Cells)
            {
                var strength = cell.Value.Length();
                if (cell.Reach <= 0f || strength < MinStrength)
                    continue;

                var chance = (0.3f + 0.7f * strength) * cell.Reach * scale;

                if (_random.NextFloat() < FogRate * chance)
                    SpawnFog(flow, tile, _random.NextFloat(0.07f, 0.15f), _random.NextFloat(0.28f, 0.56f));

                if (_random.NextFloat() < DustRate * chance)
                    SpawnDust(flow, tile, ParticleKind.Dust);

                if (strength >= GrainMinStrength && _random.NextFloat() < GrainRate * cell.Reach * scale)
                    SpawnDust(flow, tile, ParticleKind.Grain);

                if (_random.NextFloat() < WispRate * chance && TryGetGasTint(flow.Uid, tile, out var tint))
                    SpawnWisp(flow, tile, tint);

                if (!_reducedMotion && _random.NextFloat() < HazeRate * chance)
                    SpawnHaze(flow, tile, strength);
            }
        }
    }

    private Vector2 RandomPoint(Vector2i tile)
    {
        return new Vector2(tile.X + _random.NextFloat(), tile.Y + _random.NextFloat());
    }

    private void SpawnFog(GridFlow flow, Vector2i tile, float alpha, float size)
    {
        var color = FogColor;
        if (TryGetGasTint(flow.Uid, tile, out var tint))
        {
            color = tint;
            alpha *= GasFogAlpha;
        }

        var pos = RandomPoint(tile);
        Add(flow, new Particle
        {
            Kind = ParticleKind.Fog,
            Pos = pos,
            Vel = Sample(flow, pos) * (FlowSpeed * 0.5f),
            Life = _random.NextFloat(1.6f, 3f),
            Size = size,
            Alpha = alpha,
            Color = color,
            Mobility = 0.6f,
            Relax = 5f,
        });
    }

    private void SpawnDust(GridFlow flow, Vector2i tile, ParticleKind kind)
    {
        Add(flow, new Particle
        {
            Kind = kind,
            Pos = RandomPoint(tile),
            Life = _random.NextFloat(1.6f, 3.4f),
            Mobility = kind == ParticleKind.Dust ? _random.NextFloat(0.7f, 0.95f) : _random.NextFloat(0.35f, 0.5f),
            Relax = 4f,
        });
    }

    private void SpawnWisp(GridFlow flow, Vector2i tile, Color color)
    {
        var pos = RandomPoint(tile);
        Add(flow, new Particle
        {
            Kind = ParticleKind.Wisp,
            Pos = pos,
            Vel = Sample(flow, pos) * FlowSpeed,
            Life = _random.NextFloat(0.5f, 1.3f),
            Color = Color.InterpolateBetween(color, Color.White, WispBrightening),
            Mobility = 1f,
            Relax = 14f,
        });
    }

    private void SpawnHaze(GridFlow flow, Vector2i tile, float strength)
    {
        var pos = RandomPoint(tile);
        Add(flow, new Particle
        {
            Kind = ParticleKind.Haze,
            Pos = pos,
            Vel = Sample(flow, pos) * (FlowSpeed * 0.8f),
            Life = _random.NextFloat(0.9f, 1.5f),
            Size = _random.NextFloat(1.1f, 1.6f),
            Alpha = MathF.Min(1f, 0.35f + strength),
            Mobility = 0.8f,
            Relax = 6f,
        });
    }

    private void Add(GridFlow flow, Particle particle)
    {
        if (_count >= MaxParticles)
            return;

        particle.Grid = flow.Uid;
        _particles[_count++] = particle;
        flow.Particles++;

        if (particle.Kind == ParticleKind.Haze)
            HazeCount++;
    }

    private void UpdateParticles(float frameTime)
    {
        GridFlow? flow = null;
        var grid = EntityUid.Invalid;

        for (var i = _count - 1; i >= 0; i--)
        {
            ref var particle = ref _particles[i];
            if (particle.Grid != grid)
            {
                grid = particle.Grid;
                _grids.TryGetValue(grid, out flow);
            }

            if (flow != null && flow.Fade > 0f && Advance(ref particle, flow, frameTime))
                continue;

            if (flow != null)
                flow.Particles--;

            if (particle.Kind == ParticleKind.Haze)
                HazeCount--;

            _particles[i] = _particles[--_count];
        }
    }

    private bool Advance(ref Particle particle, GridFlow flow, float frameTime)
    {
        particle.Age += frameTime;
        if (particle.Age >= particle.Life)
            return false;

        // Past the breach there is no flow to follow, the particle coasts and fades.
        if (particle.Out > 0f)
        {
            particle.Out += frameTime;
            particle.Vel *= MathF.Exp(-OutDrag * frameTime);
            particle.Pos += particle.Vel * frameTime;
            return particle.Out < GetOutLife(particle.Kind);
        }

        var tile = GetTile(particle.Pos);
        var known = flow.Cells.TryGetValue(tile, out var cell);
        if (!known && IsVoid(flow, tile))
        {
            particle.Out = frameTime;
            var side = new Vector2(-particle.Vel.Y, particle.Vel.X);
            particle.Vel += side * (_random.NextFloat(-1f, 1f) * OutSpread);
            particle.Pos += particle.Vel * frameTime;
            return true;
        }

        // The breach this particle was heading for is sealed while another one keeps the grid flowing.
        if (!known || cell!.Reach <= 0f)
            particle.Age += frameTime * StrandedAging;

        var target = Sample(flow, particle.Pos) * (FlowSpeed * particle.Mobility);
        particle.Vel += (target - particle.Vel) * (1f - MathF.Exp(-particle.Relax * frameTime));

        var next = particle.Pos + particle.Vel * frameTime;
        var nextTile = GetTile(next);
        if (nextTile != tile && !CanEnter(flow, nextTile))
        {
            var alongX = new Vector2i(nextTile.X, tile.Y);
            var alongY = new Vector2i(tile.X, nextTile.Y);

            if (alongX == tile || CanEnter(flow, alongX))
            {
                next.Y = particle.Pos.Y;
                particle.Vel.Y *= -0.2f;
            }
            else if (alongY == tile || CanEnter(flow, alongY))
            {
                next.X = particle.Pos.X;
                particle.Vel.X *= -0.2f;
            }
            else
            {
                next = particle.Pos;
                particle.Vel = Vector2.Zero;
            }
        }

        particle.Pos = next;
        return true;
    }

    private static Vector2i GetTile(Vector2 pos)
    {
        return new Vector2i((int) MathF.Floor(pos.X), (int) MathF.Floor(pos.Y));
    }

    internal static float GetOutLife(ParticleKind kind)
    {
        return kind switch
        {
            ParticleKind.Fog => 1.1f,
            ParticleKind.Wisp => 0.6f,
            ParticleKind.Haze => 0.5f,
            _ => 2.2f,
        };
    }

    private bool TryGetGasTint(EntityUid grid, Vector2i tile, out Color color)
    {
        color = default;

        if (!TryComp(grid, out GasTileOverlayComponent? overlay)
            || !overlay.Chunks.TryGetValue(SharedGasTileOverlaySystem.GetGasChunkIndices(tile), out var chunk))
        {
            return false;
        }

        var index = tile.X - chunk.Origin.X + (tile.Y - chunk.Origin.Y) * SharedGasTileOverlaySystem.ChunkSize;
        var opacity = chunk.TileData[index].Opacity;
        if (opacity == null)
            return false;

        var best = -1;
        for (var i = 0; i < opacity.Length && i < _gasOverlay.VisibleGasId.Length; i++)
        {
            if (opacity[i] > 0 && (best < 0 || opacity[i] > opacity[best]))
                best = i;
        }

        if (best < 0)
            return false;

        var gasId = _gasOverlay.VisibleGasId[best];
        if (!_gasTints.TryGetValue(gasId, out color))
        {
            var gas = _atmos.GetGas(gasId);
            color = _proto.TryIndex<AirFlowGasTintPrototype>(gas.ID, out var tint) ? tint.Color : gas.Color;
            _gasTints[gasId] = color;
        }

        return true;
    }

    internal enum ParticleKind : byte
    {
        Fog,
        Dust,
        Grain,
        Wisp,
        Haze,
    }

    internal struct Particle
    {
        public EntityUid Grid;
        public ParticleKind Kind;
        public Vector2 Pos;
        public Vector2 Vel;
        public float Age;
        public float Life;
        public float Size;
        public float Alpha;
        public Color Color;
        public float Mobility;
        public float Relax;
        public float Out;
    }
}
