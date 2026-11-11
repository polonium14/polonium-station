using System.Numerics;
using Content.Client._Starlight.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.Map;
using Robust.Shared.Utility;
using TimedDespawnComponent = Robust.Shared.Spawners.TimedDespawnComponent;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    [Dependency] private ImpactVisualsSystem _impactVisuals = default!;

    // The real shot is instant, so the beam is slowed down enough to be seen travelling.
    private const float BeamMsPerTile = 5000f / 315f;
    private const float TailMsPerTile = BeamMsPerTile / 2f;
    private const float FlashFadeMs = 100f;
    private const float BeamWidth = 1f;

    private void RenderHitscan(HitscanEvent ev)
    {
        var origin = GetCoordinates(ev.Origin);
        if (!Exists(origin.EntityId))
            return;

        var direction = ev.Angle.ToVec();
        var length = ev.Distance * BeamMsPerTile;
        var travel = origin.Offset(direction * (ev.Distance + 0.5f) / 2);
        var travelDistance = ev.Distance - 1.5f;

        if (ev.Distance >= 1f)
        {
            if (ev.MuzzleFlash != null)
                RenderFlash(origin.Offset(direction / 2), ev.Angle, ev.MuzzleFlash, 1f, false, false, length, 0f);

            if (ev.TravelFlash != null)
                RenderFlash(travel, ev.Angle, ev.TravelFlash, travelDistance, true, false, length, 0f);
        }

        var impact = origin.Offset(direction * ev.Distance);
        Timer.Spawn((int) length, () =>
        {
            if (!Exists(impact.EntityId))
                return;

            var tail = 0f;
            if (ev.TravelFlash != null && ev.Distance >= 1f)
                tail = RenderTail(travel, ev.Angle, ev.TravelFlash, travelDistance);

            if (ev.ImpactFlash != null)
                RenderFlash(impact, ev.Angle, ev.ImpactFlash, 1f, false, true, length, tail);

            if (TryGetEntity(ev.HitEntity, out var target) && Exists(target))
                _impactVisuals.PlayImpact(impact, ev.Angle, target.Value);
        });
    }

    private bool TrySpawnBeamEffect(EntityCoordinates coords, Angle angle, SpriteSpecifier.Rsi rsi, out Entity<SpriteComponent> effect)
    {
        effect = default;
        if (!TryComp(coords.EntityId, out TransformComponent? relativeXform))
            return false;

        var ent = Spawn(HitscanProto, coords);
        effect = (ent, Comp<SpriteComponent>(ent));

        var xform = Transform(ent);
        var targetWorldRot = angle + _xform.GetWorldRotation(relativeXform);
        var delta = targetWorldRot - _xform.GetWorldRotation(xform);
        _xform.SetLocalRotationNoLerp(ent, xform.LocalRotation + delta, xform);

        _sprite.LayerSetAutoAnimated(effect.AsNullable(), EffectLayers.Unshaded, false);
        _sprite.LayerSetSprite(effect.AsNullable(), EffectLayers.Unshaded, rsi);
        _sprite.LayerSetRsiState(effect.AsNullable(), EffectLayers.Unshaded, rsi.RsiState);
        _sprite.LayerSetVisible(effect.AsNullable(), EffectLayers.Unshaded, true);
        return true;
    }

    private void RenderFlash(EntityCoordinates coords, Angle angle, SpriteSpecifier sprite, float distance, bool travel, bool end, float length, float delay)
    {
        if (sprite is not SpriteSpecifier.Rsi rsi || !TrySpawnBeamEffect(coords, angle, rsi, out var ent))
            return;

        if (end)
            length = 0f;

        var time = delay + length + FlashFadeMs;

        if (travel)
        {
            _sprite.SetScale(ent.AsNullable(), new Vector2(0.05f, BeamWidth));
            _sprite.SetOffset(ent.AsNullable(), new Vector2(distance * -0.5f, 0f));
        }
        else
        {
            _sprite.SetScale(ent.AsNullable(), new Vector2(1f, BeamWidth));
        }

        Comp<TimedDespawnComponent>(ent).Lifetime = time / 1000f;

        var anim = new Animation
        {
            Length = TimeSpan.FromMilliseconds(time),
            AnimationTracks =
            {
                new AnimationTrackSpriteFlick
                {
                    LayerKey = EffectLayers.Unshaded,
                    KeyFrames =
                    {
                        new AnimationTrackSpriteFlick.KeyFrame(rsi.RsiState, (time - FlashFadeMs) / 1000f),
                    },
                },
            },
        };

        if (travel)
        {
            anim.AnimationTracks.Add(new AnimationTrackComponentProperty
            {
                ComponentType = typeof(SpriteComponent),
                Property = nameof(SpriteComponent.Scale),
                InterpolationMode = AnimationInterpolationMode.Cubic,
                KeyFrames =
                {
                    new AnimationTrackProperty.KeyFrame(new Vector2(0.05f, BeamWidth), delay / 1000f),
                    new AnimationTrackProperty.KeyFrame(new Vector2(distance, BeamWidth), length / 1000f),
                    new AnimationTrackProperty.KeyFrame(new Vector2(distance, BeamWidth), FlashFadeMs / 1000f),
                },
            });
            anim.AnimationTracks.Add(new AnimationTrackComponentProperty
            {
                ComponentType = typeof(SpriteComponent),
                Property = nameof(SpriteComponent.Offset),
                InterpolationMode = AnimationInterpolationMode.Cubic,
                KeyFrames =
                {
                    new AnimationTrackProperty.KeyFrame(new Vector2(distance * -0.5f, 0f), delay / 1000f),
                    new AnimationTrackProperty.KeyFrame(Vector2.Zero, length / 1000f),
                    new AnimationTrackProperty.KeyFrame(Vector2.Zero, FlashFadeMs / 1000f),
                },
            });
        }

        _animPlayer.Play(ent, anim, "hitscan-effect");
    }

    /// <summary>
    /// Draws the thin beam left behind after the flash and pulls its tail into the target.
    /// Returns how long that takes, in milliseconds.
    /// </summary>
    private float RenderTail(EntityCoordinates coords, Angle angle, SpriteSpecifier sprite, float distance)
    {
        if (distance <= 0f || sprite is not SpriteSpecifier.Rsi rsi || !TrySpawnBeamEffect(coords, angle, rsi, out var ent))
            return 0f;

        var time = distance * TailMsPerTile;

        ent.Comp.RenderOrder = 1;
        _sprite.SetScale(ent.AsNullable(), new Vector2(distance, BeamWidth));
        Comp<TimedDespawnComponent>(ent).Lifetime = time / 1000f;

        var anim = new Animation
        {
            Length = TimeSpan.FromMilliseconds(time),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Scale),
                    InterpolationMode = AnimationInterpolationMode.Cubic,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(new Vector2(distance, BeamWidth), 0f),
                        new AnimationTrackProperty.KeyFrame(new Vector2(0.05f, BeamWidth), time / 1000f),
                    },
                },
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    InterpolationMode = AnimationInterpolationMode.Cubic,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0f),
                        new AnimationTrackProperty.KeyFrame(new Vector2(distance * 0.5f, 0f), time / 1000f),
                    },
                },
            },
        };

        _animPlayer.Play(ent, anim, "hitscan-tail");
        return time;
    }
}
