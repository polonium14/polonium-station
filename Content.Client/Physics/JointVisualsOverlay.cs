using System.Numerics;
using Content.Shared.Physics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Graphics.RSI; // POLONIUM
using Robust.Shared.Timing; // POLONIUM

namespace Content.Client.Physics;

/// <summary>
/// Draws a texture on top of a joint.
/// </summary>
public sealed class JointVisualsOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    private IEntityManager _entManager;
    private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>(); // POLONIUM

    public JointVisualsOverlay(IEntityManager entManager)
    {
        _entManager = entManager;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var worldHandle = args.WorldHandle;

        var spriteSystem = _entManager.System<SpriteSystem>();
        var xformSystem = _entManager.System<SharedTransformSystem>();
        var joints = _entManager.EntityQueryEnumerator<JointVisualsComponent, TransformComponent>();
        var xformQuery = _entManager.GetEntityQuery<TransformComponent>();

        args.DrawingHandle.SetTransform(Matrix3x2.Identity);

        while (joints.MoveNext(out var visuals, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            var other = visuals.Target;

            if (!xformQuery.TryGetComponent(other, out var otherXform))
                continue;

            if (xform.MapID != otherXform.MapID)
                continue;

            var texture = GetFrame(spriteSystem.RsiStateLike(visuals.Sprite)); // POLONIUM: animated lines
            var width = texture.Width / (float)EyeManager.PixelsPerMeter;

            var coordsA = xform.Coordinates;
            var coordsB = otherXform.Coordinates;

            var rotA = xform.LocalRotation;
            var rotB = otherXform.LocalRotation;

            coordsA = coordsA.Offset(rotA.RotateVec(visuals.OffsetA));
            coordsB = coordsB.Offset(rotB.RotateVec(visuals.OffsetB));

            var posA = xformSystem.ToMapCoordinates(coordsA).Position;
            var posB = xformSystem.ToMapCoordinates(coordsB).Position;
            var diff = posB - posA;
            var length = diff.Length();

            var midPoint = diff / 2f + posA;
            var angle = (posB - posA).ToWorldAngle();
            var box = new Box2(-width / 2f, -length / 2f, width / 2f, length / 2f);
            var rotate = new Box2Rotated(box.Translated(midPoint), angle, midPoint);

            worldHandle.DrawTextureRect(texture, rotate);
        }
    }

    // POLONIUM start
    private Texture GetFrame(IRsiStateLike state)
    {
        if (!state.IsAnimated)
            return state.Default;

        var total = 0f;
        for (var i = 0; i < state.AnimationFrameCount; i++)
        {
            total += state.GetDelay(i);
        }

        if (total <= 0f)
            return state.Default;

        var time = (float) (_timing.RealTime.TotalSeconds % total);
        for (var i = 0; i < state.AnimationFrameCount; i++)
        {
            time -= state.GetDelay(i);
            if (time <= 0f)
                return state.GetFrame(RsiDirection.South, i);
        }

        return state.Default;
    }
    // POLONIUM end
}
