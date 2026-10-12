using System.Numerics;
using Content.Shared._Polonium.Replicator;
using Content.Shared.Gravity;
using Robust.Client.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Replicator;

public sealed partial class ReplicatorColossusVisualsSystem : EntitySystem
{
    private const float StopDelay = 0.15f;
    private const float MinSpeed = 0.25f;
    private const float MaxStep = 1f;
    private const float VelocitySmoothing = 12f;

    private static readonly ReplicatorColossusVisualLayers[] LaunchLayers =
        [ReplicatorColossusVisualLayers.Launch, ReplicatorColossusVisualLayers.LaunchGlow];

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ReplicatorColossusGaitComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var gait, out var sprite, out var xform))
        {
            if (!_sprite.TryGetLayer((uid, sprite), ReplicatorColossusVisualLayers.Body, out var body, false))
                continue;

            if (UpdateLaunch((uid, sprite), now))
            {
                _sprite.LayerSetVisible(body, false);
                UpdateThrust((uid, sprite), body, false);
                gait.Tracking = false;
                continue;
            }

            _sprite.LayerSetVisible(body, true);
            UpdateGait((uid, gait, sprite, xform), body, frameTime, now);
        }
    }

    private bool UpdateLaunch(Entity<SpriteComponent> ent, TimeSpan now)
    {
        TryComp<ReplicatorColossusLaunchComponent>(ent, out var launch);
        var shown = false;
        foreach (var key in LaunchLayers)
        {
            if (!_sprite.TryGetLayer(ent.AsNullable(), key, out var layer, false))
                continue;

            _sprite.LayerSetVisible(layer, launch != null);
            if (launch == null)
                continue;

            shown = true;
            _sprite.LayerSetAutoAnimated(layer, false);

            var length = layer.ActualState?.TotalDelay ?? 0f;
            var elapsed = (float) (now - launch.StartTime).TotalSeconds;
            _sprite.LayerSetAnimationTime(layer, Math.Clamp(elapsed, 0f, MathF.Max(0f, length - 0.001f)));
        }

        return shown;
    }

    private void UpdateGait(Entity<ReplicatorColossusGaitComponent, SpriteComponent, TransformComponent> ent,
        SpriteComponent.Layer body,
        float frameTime,
        TimeSpan now)
    {
        var gait = ent.Comp1;
        var position = _transform.GetWorldPosition(ent.Comp3);
        var step = position - gait.LastPosition;
        gait.LastPosition = position;

        if (!gait.Tracking || step.Length() > MaxStep || frameTime <= 0f)
        {
            gait.Tracking = true;
            gait.Velocity = Vector2.Zero;
            step = Vector2.Zero;
        }
        else
        {
            var blend = 1f - MathF.Exp(-VelocitySmoothing * frameTime);
            gait.Velocity = Vector2.Lerp(gait.Velocity, step / frameTime, blend);
        }

        if (step.LengthSquared() > 0f)
            gait.LastMoved = now;

        var floating = !_gravity.EntityGridOrMapHaveGravity((ent.Owner, ent.Comp3));
        var moving = (now - gait.LastMoved).TotalSeconds < StopDelay && gait.Velocity.Length() > MinSpeed;

        if (floating || !moving)
        {
            SetState(ent, body, false, floating ? "fly" : "idle");
            _sprite.LayerSetAutoAnimated(body, true);
            UpdateThrust((ent.Owner, ent.Comp2), body, floating);
            return;
        }

        UpdateThrust((ent.Owner, ent.Comp2), body, false);

        var facing = _transform.GetWorldRotation(ent.Comp3);
        var relative = (gait.Velocity.ToWorldAngle() - facing).Reduced();
        var (strafe, reversed) = (int) Math.Round(relative.Degrees / 90) switch
        {
            1 => (true, false),
            -1 => (true, true),
            2 or -2 => (false, true),
            _ => (false, false),
        };

        SetState(ent, body, strafe, strafe ? "walk_l90" : "walk");
        _sprite.LayerSetAutoAnimated(body, false);

        if (gait.TilesPerLoop <= 0f || body.ActualState is not { } rsiState)
            return;

        var previous = gait.Phase;
        gait.Phase += (reversed ? -1f : 1f) * step.Length() / gait.TilesPerLoop;

        if (MathF.Floor(previous * gait.StepsPerLoop) != MathF.Floor(gait.Phase * gait.StepsPerLoop))
            _audio.PlayEntity(gait.StepSound, Filter.Local(), ent, false);

        gait.Phase -= MathF.Floor(gait.Phase);
        _sprite.LayerSetAnimationTime(body, gait.Phase * rsiState.TotalDelay);
    }

    private void UpdateThrust(Entity<SpriteComponent> ent, SpriteComponent.Layer body, bool flying)
    {
        if (!_sprite.TryGetLayer(ent.AsNullable(), ReplicatorColossusVisualLayers.Thrust, out var thrust, false))
            return;

        _sprite.LayerSetVisible(thrust, flying);
        if (!flying)
            return;

        _sprite.LayerSetAutoAnimated(thrust, false);
        _sprite.LayerSetAnimationTime(thrust, body.AnimationTime);
    }

    private void SetState(Entity<ReplicatorColossusGaitComponent, SpriteComponent, TransformComponent> ent,
        SpriteComponent.Layer body,
        bool strafe,
        string state)
    {
        var gait = ent.Comp1;
        if (strafe != gait.StrafeSpriteActive)
        {
            gait.StrafeSpriteActive = strafe;
            _sprite.LayerSetRsi(body, strafe ? gait.StrafeSprite : gait.Sprite, state);
            return;
        }

        _sprite.LayerSetRsiState(body, state);
    }
}
