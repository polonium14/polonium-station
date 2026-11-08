using System.Numerics;
using Content.Shared._Polonium.Weapons.Ranged;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Weapons.Ranged;

public sealed partial class ProjectileTracerSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const float PointSpacing = 0.08f;
    private const float MaxJump = 3f;
    private const int MaxPoints = 96;

    private readonly Dictionary<EntityUid, TracerTrail> _attached = new();
    private readonly List<TracerTrail> _trails = new();
    private readonly Stack<TracerTrail> _pool = new();
    private readonly List<EntityUid> _lost = new();
    private uint _frame;
    private int _seed;

    public IReadOnlyList<TracerTrail> Trails => _trails;

    public override void Initialize()
    {
        base.Initialize();
        _overlay.AddOverlay(new ProjectileTracerOverlay(this));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<ProjectileTracerOverlay>();
        _attached.Clear();
        _trails.Clear();
        _pool.Clear();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var now = _timing.RealTime;
        _frame++;

        var query = EntityQueryEnumerator<ProjectileTracerComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var tracer, out var sprite, out var xform))
        {
            // the shooter's server copy stays hidden behind its predicted twin, which draws its own streak
            if (!sprite.Visible || xform.MapID == MapId.Nullspace)
                continue;

            var position = _transform.GetWorldPosition(xform);
            if (_attached.TryGetValue(uid, out var trail) &&
                (trail.Map != xform.MapID || Vector2.DistanceSquared(trail.Head, position) > MaxJump * MaxJump))
            {
                // portals and other teleports start a new streak instead of drawing one across the map
                Detach(trail, now);
                _attached.Remove(uid);
                trail = null;
            }

            if (trail == null)
            {
                trail = Rent(tracer, xform.MapID, position, now);
                _attached.Add(uid, trail);
                _trails.Add(trail);
            }

            trail.Head = position;
            trail.Frame = _frame;

            if (Vector2.DistanceSquared(trail.Points[^1].Position, position) >= PointSpacing * PointSpacing)
            {
                if (trail.Points.Count >= MaxPoints)
                    trail.Points.RemoveAt(0);

                trail.Points.Add(new TracerPoint(position, now));
            }
        }

        foreach (var (uid, trail) in _attached)
        {
            if (trail.Frame != _frame)
                _lost.Add(uid);
        }

        foreach (var uid in _lost)
        {
            Detach(_attached[uid], now);
            _attached.Remove(uid);
        }

        _lost.Clear();

        for (var i = _trails.Count - 1; i >= 0; i--)
        {
            var trail = _trails[i];
            var points = trail.Points;

            while (points.Count > 2 && now - points[1].Time > trail.FadeTime)
            {
                points.RemoveAt(0);
            }

            if (trail.Attached || now - points[^1].Time <= trail.FadeTime)
                continue;

            _trails[i] = _trails[^1];
            _trails.RemoveAt(_trails.Count - 1);
            _pool.Push(trail);
        }
    }

    private TracerTrail Rent(ProjectileTracerComponent tracer, MapId map, Vector2 position, TimeSpan now)
    {
        var trail = _pool.Count > 0 ? _pool.Pop() : new TracerTrail();
        trail.Points.Clear();
        trail.Points.Add(new TracerPoint(position, now));
        trail.Head = position;
        trail.Map = map;
        trail.Attached = true;
        trail.Seed = _seed++;
        trail.Style = tracer.Style;
        trail.Color = tracer.Color;
        trail.Length = tracer.Length;
        trail.Width = tracer.Width;
        trail.FadeTime = tracer.FadeTime;
        return trail;
    }

    private static void Detach(TracerTrail trail, TimeSpan now)
    {
        trail.Attached = false;
        if (trail.Points[^1].Position != trail.Head)
            trail.Points.Add(new TracerPoint(trail.Head, now));
    }
}

public sealed class TracerTrail
{
    /// <summary>
    /// Positions the projectile passed, oldest first.
    /// </summary>
    public readonly List<TracerPoint> Points = new();

    public Vector2 Head;
    public MapId Map;
    public bool Attached;
    public uint Frame;
    public int Seed;
    public TracerStyle Style;
    public Color Color;
    public float Length;
    public float Width;
    public TimeSpan FadeTime;
}

public readonly record struct TracerPoint(Vector2 Position, TimeSpan Time);
