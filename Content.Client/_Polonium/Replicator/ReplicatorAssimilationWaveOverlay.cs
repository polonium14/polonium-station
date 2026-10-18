using Content.Shared._Polonium.Replicator;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Replicator;

public sealed partial class ReplicatorAssimilationWaveOverlay : Overlay
{
    private const float FrontWidth = 2.5f;
    private const int Rings = 10;

    private static readonly Color FrontColor = Color.FromHex("#ffff91");
    private static readonly Color TrailColor = Color.FromHex("#d70aa0");

    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly SharedTransformSystem _transform;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public ReplicatorAssimilationWaveOverlay()
    {
        IoCManager.InjectDependencies(this);
        _transform = _entity.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var query = _entity.EntityQueryEnumerator<ReplicatorAssimilationWaveComponent, TransformComponent>();
        while (query.MoveNext(out _, out var wave, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            var radius = MathF.Min((float) (_timing.CurTime - wave.StartTime).TotalSeconds * wave.Speed, wave.MaxRadius);
            if (radius <= 0f)
                continue;

            var origin = _transform.GetWorldPosition(xform);
            for (var i = 0; i < Rings; i++)
            {
                var fraction = i / (float) (Rings - 1);
                var ring = radius - fraction * FrontWidth;
                if (ring <= 0f)
                    break;

                var color = Color.InterpolateBetween(FrontColor, TrailColor, fraction).WithAlpha(0.9f * (1f - fraction));
                args.WorldHandle.DrawCircle(origin, ring, color, false);
            }
        }
    }
}

public sealed partial class ReplicatorAssimilationWaveSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay.AddOverlay(new ReplicatorAssimilationWaveOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay<ReplicatorAssimilationWaveOverlay>();
    }
}
