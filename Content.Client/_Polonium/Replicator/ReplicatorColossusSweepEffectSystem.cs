using Content.Shared._Polonium.Replicator;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Replicator;

public sealed partial class ReplicatorColossusSweepEffectSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ReplicatorColossusSweepEffectComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var sweep, out var sprite, out var xform))
        {
            var progress = sweep.Duration > TimeSpan.Zero
                ? Math.Clamp((now - sweep.StartTime) / sweep.Duration, 0.0, 1.0)
                : 1.0;

            var angle = sweep.StartAngle + Angle.FromDegrees(360.0 * progress) - _transform.GetWorldRotation(xform);
            _sprite.SetRotation((uid, sprite), angle);
        }
    }
}
