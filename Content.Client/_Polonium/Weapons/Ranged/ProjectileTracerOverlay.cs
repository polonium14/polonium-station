using System.Numerics;
using System.Runtime.InteropServices;
using Content.Shared._Polonium.Weapons.Ranged;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._Polonium.Weapons.Ranged;

public sealed partial class ProjectileTracerOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Shader = "PoloniumTracer";

    private const float LaserSolid = 0.7f;

    private const float ArcStep = 0.12f;
    private const float ArcAnchor = 0.3f;
    private const float ArcAmplitude = 3f;
    private const float ArcFlicker = 24f;
    private const float ArcSecondStrand = 0.5f;

    private const int TrailVertexBudget = 2048;
    private const int MaxVertices = 60000;

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    private readonly ProjectileTracerSystem _system;
    private readonly ShaderInstance _shader;
    private readonly Texture _white;

    private readonly List<(Vector2 Position, float Fade, float Distance)> _path = new();
    private readonly List<(Vector2 Position, float Fade, float Distance)> _arc = new();
    private readonly List<(Vector2 Position, float Fade, float Distance)> _split = new();
    private readonly List<DrawVertexUV2DColor> _vertices = new();
    private readonly List<ushort> _indices = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    public ProjectileTracerOverlay(ProjectileTracerSystem system)
    {
        IoCManager.InjectDependencies(this);
        _system = system;
        _shader = _prototype.Index(Shader).Instance();
        _white = Texture.White;
        // under the bullet sprites, so the bullet flies at the head of its own streak
        ZIndex = (int)DrawDepth.Objects;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var trails = _system.Trails;
        if (trails.Count == 0)
            return;

        var now = _timing.RealTime;
        var time = (float)(now.TotalSeconds % 3600);
        var handle = args.WorldHandle;
        handle.UseShader(_shader);
        handle.SetTransform(Matrix3x2.Identity);

        foreach (var trail in trails)
        {
            if (trail.Map != args.MapId || !args.WorldAABB.Enlarged(trail.Length).Contains(trail.Head))
                continue;

            if (_vertices.Count > MaxVertices - TrailVertexBudget)
                Flush(handle);

            var color = Color.FromSrgb(trail.Color);
            switch (trail.Style)
            {
                case TracerStyle.Electric:
                    AddElectric(trail, color, now, time);
                    break;
                default:
                    BuildPath(trail, now);
                    if (_path.Count >= 2)
                        AddLine(_path, trail.Width, color, trail.Style == TracerStyle.Laser ? LaserSolid : 0f);
                    break;
            }
        }

        Flush(handle);
        handle.UseShader(null);
    }

    private void AddElectric(TracerTrail trail, Color color, TimeSpan now, float time)
    {
        BuildPath(trail, now);
        if (_path.Count < 2)
            return;

        // two strands that crackle into a new shape several times a second
        var tick = (int)(time * ArcFlicker);
        BuildArc(trail.Width * ArcAmplitude, trail.Seed * 2, tick);
        AddLine(_arc, trail.Width, color, 0f);
        BuildArc(trail.Width * ArcAmplitude, trail.Seed * 2 + 1, tick);
        AddLine(_arc, trail.Width * ArcSecondStrand, color.WithAlpha(color.A * ArcSecondStrand), 0f);
    }

    /// <summary>
    /// Copies the path into a jagged line that zigzags around it, pinned to the projectile at its head.
    /// </summary>
    private void BuildArc(float amplitude, int seed, int tick)
    {
        _arc.Clear();

        var total = _path[^1].Distance;
        if (total <= 0f)
            return;

        var steps = Math.Max(2, (int)MathF.Ceiling(total / ArcStep));
        var segment = 1;
        for (var step = 0; step <= steps; step++)
        {
            var distance = total * step / steps;
            while (segment < _path.Count - 1 && _path[segment].Distance < distance)
            {
                segment++;
            }

            var (from, fromFade, fromDistance) = _path[segment - 1];
            var (to, toFade, toDistance) = _path[segment];
            var t = toDistance > fromDistance ? (distance - fromDistance) / (toDistance - fromDistance) : 0f;
            var position = Vector2.Lerp(from, to, t);
            var fade = fromFade + (toFade - fromFade) * t;

            var direction = from - to;
            if (step > 0 && direction.LengthSquared() > 0f)
            {
                var normal = new Vector2(-direction.Y, direction.X) / direction.Length();
                var anchor = MathF.Min(1f, distance / ArcAnchor);
                position += normal * amplitude * anchor * Noise(step, tick, seed);
            }

            _arc.Add((position, fade, distance));
        }
    }

    /// <summary>
    /// Cheap hash noise in the range -1..1, the same for the same arguments.
    /// </summary>
    private static float Noise(int a, int b, int c)
    {
        var hash = (uint)a * 73856093u ^ (uint)b * 19349663u ^ (uint)c * 83492791u;
        hash ^= hash >> 13;
        hash *= 0x5bd1e995u;
        hash ^= hash >> 15;
        return (hash & 0xFFFF) / 32767.5f - 1f;
    }

    /// <summary>
    /// Walks the trail from the projectile back to where its streak ends,
    /// fading each point by its distance from the head and by its age.
    /// </summary>
    private void BuildPath(TracerTrail trail, TimeSpan now)
    {
        _path.Clear();

        var points = trail.Points;
        var fadeSeconds = (float)trail.FadeTime.TotalSeconds;
        var previous = trail.Attached ? trail.Head : points[^1].Position;
        var previousTime = trail.Attached ? now : points[^1].Time;
        var previousFade = (float)(now - previousTime).TotalSeconds / fadeSeconds;
        if (previousFade >= 1f)
            return;

        _path.Add((previous, previousFade, 0f));

        var distance = 0f;
        for (var i = trail.Attached ? points.Count - 1 : points.Count - 2; i >= 0; i--)
        {
            var point = points[i];
            var segment = Vector2.Distance(previous, point.Position);
            if (segment < 0.001f)
                continue;

            var fade = MathF.Max((distance + segment) / trail.Length, (float)(now - point.Time).TotalSeconds / fadeSeconds);
            if (fade >= 1f)
            {
                var cut = (1f - previousFade) / (fade - previousFade);
                _path.Add((Vector2.Lerp(previous, point.Position, cut), 1f, distance + segment * cut));
                return;
            }

            distance += segment;
            _path.Add((point.Position, fade, distance));
            previous = point.Position;
            previousFade = fade;
        }

        // the streak still starts at the muzzle, taper it there too
        _path[^1] = (_path[^1].Position, 1f, _path[^1].Distance);
    }

    /// <summary>
    /// Adds a thin solid line along the path that keeps full strength over its solid part and fades out towards its tail.
    /// </summary>
    private void AddLine(List<(Vector2 Position, float Fade, float Distance)> path, float width, Color color, float solid)
    {
        if (solid > 0f)
        {
            SplitAt(path, solid);
            path = _split;
        }

        var start = _vertices.Count;

        for (var i = 0; i < path.Count; i++)
        {
            var position = path[i].Position;
            var from = i == 0 ? position : path[i - 1].Position;
            var to = i == path.Count - 1 ? position : path[i + 1].Position;
            var direction = from - to;
            var normal = direction.LengthSquared() > 0f
                ? new Vector2(-direction.Y, direction.X) / direction.Length() * width * 0.5f
                : Vector2.Zero;

            var strength = Math.Clamp((1f - path[i].Fade) / (1f - solid), 0f, 1f);
            var paint = Paint(color, color.A * strength);
            _vertices.Add(new DrawVertexUV2DColor(position + normal, paint));
            _vertices.Add(new DrawVertexUV2DColor(position - normal, paint));

            if (i == 0)
                continue;

            var a = (ushort)(start + (i - 1) * 2);
            var b = (ushort)(start + i * 2);
            _indices.Add(a);
            _indices.Add((ushort)(a + 1));
            _indices.Add(b);
            _indices.Add((ushort)(a + 1));
            _indices.Add((ushort)(b + 1));
            _indices.Add(b);
        }
    }

    private void SplitAt(List<(Vector2 Position, float Fade, float Distance)> path, float fade)
    {
        _split.Clear();

        for (var i = 0; i < path.Count; i++)
        {
            if (i > 0)
            {
                var (from, fromFade, fromDistance) = path[i - 1];
                var (to, toFade, toDistance) = path[i];
                if ((fromFade - fade) * (toFade - fade) < 0f)
                {
                    var t = (fade - fromFade) / (toFade - fromFade);
                    _split.Add((Vector2.Lerp(from, to, t), fade, fromDistance + (toDistance - fromDistance) * t));
                }
            }

            _split.Add(path[i]);
        }
    }

    /// <summary>
    /// Premultiplied color that covers what is below, so the line keeps the color of its projectile.
    /// </summary>
    private static Color Paint(Color color, float intensity)
    {
        return new Color(color.R * intensity, color.G * intensity, color.B * intensity, intensity);
    }

    private void Flush(DrawingHandleWorld handle)
    {
        if (_indices.Count > 0)
        {
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList,
                _white,
                CollectionsMarshal.AsSpan(_indices),
                CollectionsMarshal.AsSpan(_vertices));
        }

        _vertices.Clear();
        _indices.Clear();
    }
}
