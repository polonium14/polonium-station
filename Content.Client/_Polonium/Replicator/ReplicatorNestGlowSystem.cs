using Content.Shared._Impstation.Replicator;
using Content.Shared._Polonium.Replicator;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Replicator;

public sealed partial class ReplicatorNestGlowSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private PointLightSystem _pointLight = default!;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var seconds = (float) _timing.RealTime.TotalSeconds;
        var query = EntityQueryEnumerator<ReplicatorNestGlowComponent, ReplicatorNestComponent, PointLightComponent>();
        while (query.MoveNext(out var uid, out var glow, out var nest, out var light))
        {
            ReplicatorNestGlowLevel? current = null;
            foreach (var level in glow.Levels)
            {
                if (level.Level <= nest.CurrentLevel && (current == null || level.Level > current.Level))
                    current = level;
            }

            if (current == null)
            {
                _pointLight.SetEnabled(uid, false, light);
                continue;
            }

            var period = (float) glow.PulsePeriod.TotalSeconds;
            var pulse = period > 0f ? MathF.Sin(seconds * MathF.Tau / period) * glow.PulseDepth : 0f;

            _pointLight.SetEnabled(uid, true, light);
            _pointLight.SetColor(uid, glow.Color, light);
            _pointLight.SetRadius(uid, current.Radius, light);
            _pointLight.SetEnergy(uid, current.Energy * (1f + pulse), light);
        }
    }
}
