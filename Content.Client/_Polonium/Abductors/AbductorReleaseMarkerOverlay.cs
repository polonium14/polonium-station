using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;

namespace Content.Client._Polonium.Abductors;

/// <summary>
/// Marks the tile finished specimens return to, for whoever is at the observation console.
/// </summary>
public sealed partial class AbductorReleaseMarkerOverlay : Overlay
{
    private const float Pixel = 1f / EyeManager.PixelsPerMeter;
    private const int Radius = 12;
    private const float Stroke = 2 * Pixel;
    private const float Dot = 4 * Pixel;

    private static readonly Color MarkColor = Color.FromHex("#ff0099");

    [Dependency] private IEntityManager _entManager = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public EntityCoordinates? Point;

    public AbductorReleaseMarkerOverlay()
    {
        IoCManager.InjectDependencies(this);
        ZIndex = 1;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (Point is not { } point
            || !_entManager.TryGetComponent(point.EntityId, out TransformComponent? xform)
            || xform.MapID != args.MapId)
        {
            return;
        }

        var handle = args.WorldHandle;
        handle.SetTransform(_entManager.System<SharedTransformSystem>().GetWorldMatrix(xform));

        var center = point.Position;
        var stroke = new Vector2(Stroke);
        for (var step = 0; step <= Radius; step++)
        {
            var offset = new Vector2(Radius - step, step) * Pixel;
            var mirrored = new Vector2(offset.X, -offset.Y);
            handle.DrawRect(Box2.CenteredAround(center + offset, stroke), MarkColor);
            handle.DrawRect(Box2.CenteredAround(center - offset, stroke), MarkColor);
            handle.DrawRect(Box2.CenteredAround(center + mirrored, stroke), MarkColor);
            handle.DrawRect(Box2.CenteredAround(center - mirrored, stroke), MarkColor);
        }

        handle.DrawRect(Box2.CenteredAround(center, new Vector2(Dot)), MarkColor);
        handle.SetTransform(Matrix3x2.Identity);
    }
}
