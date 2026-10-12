using System.Numerics;
using Content.Shared._Impstation.Replicator;
using Content.Shared._Polonium.Replicator;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Replicator;

public sealed partial class ReplicatorSensorOverlay : Overlay
{
    private const float BracketSize = 0.6f;
    private const float BracketArm = 0.22f;
    private const float ArrowDistance = 1.4f;
    private const float ArrowLength = 0.4f;
    private const float ArrowWidth = 0.22f;
    private const float MarkArrowRange = 7f;

    private static readonly Color MarkColor = Color.FromHex("#d70aa0");
    private static readonly Color TrackerColor = Color.FromHex("#ffff91");

    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;

    private readonly SharedTransformSystem _transform;
    private readonly Vector2[] _triangle = new Vector2[3];

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public ReplicatorSensorOverlay()
    {
        IoCManager.InjectDependencies(this);
        _transform = _entity.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } player
            || !_entity.HasComponent<ReplicatorComponent>(player)
            || !_entity.TryGetComponent<TransformComponent>(player, out var playerXform)
            || playerXform.MapID != args.MapId)
            return;

        var handle = args.WorldHandle;
        handle.SetTransform(Matrix3x2.Identity);

        var origin = _transform.GetWorldPosition(playerXform);
        var pulse = 0.5f + 0.5f * MathF.Sin((float) _timing.RealTime.TotalSeconds * 4f);

        var marks = _entity.EntityQueryEnumerator<ReplicatorMarkedComponent, TransformComponent>();
        while (marks.MoveNext(out _, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            var position = _transform.GetWorldPosition(xform);
            DrawBrackets(handle, position, BracketSize + 0.08f * pulse, MarkColor);

            if (Vector2.Distance(position, origin) > MarkArrowRange)
                DrawArrow(handle, origin, position, MarkColor);
        }

        if (_entity.TryGetComponent<ReplicatorTrackerComponent>(player, out var tracker)
            && tracker.Target.MapId == args.MapId
            && Vector2.Distance(tracker.Target.Position, origin) > tracker.MinDistance)
        {
            DrawArrow(handle, origin, tracker.Target.Position, TrackerColor.WithAlpha(0.6f + 0.4f * pulse));
        }
    }

    private static void DrawBrackets(DrawingHandleWorld handle, Vector2 centre, float size, Color color)
    {
        for (var x = -1; x <= 1; x += 2)
        {
            for (var y = -1; y <= 1; y += 2)
            {
                var corner = centre + new Vector2(x * size, y * size);
                handle.DrawLine(corner, corner - new Vector2(x * BracketArm, 0), color);
                handle.DrawLine(corner, corner - new Vector2(0, y * BracketArm), color);
            }
        }
    }

    private void DrawArrow(DrawingHandleWorld handle, Vector2 origin, Vector2 target, Color color)
    {
        var direction = Vector2.Normalize(target - origin);
        var side = new Vector2(-direction.Y, direction.X) * ArrowWidth;
        var foot = origin + direction * ArrowDistance;

        _triangle[0] = foot + direction * ArrowLength;
        _triangle[1] = foot + side;
        _triangle[2] = foot - side;
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _triangle, color);
    }
}

public sealed partial class ReplicatorSensorOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay.AddOverlay(new ReplicatorSensorOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay<ReplicatorSensorOverlay>();
    }
}
