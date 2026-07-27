using System.Numerics;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Emoting;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;

namespace Content.Client.Emoting;

public sealed partial class AnimatedEmotesSystem : EntitySystem
{
    [Dependency] private AnimationPlayerSystem _anim = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<AnimatedEmoteEvent>(OnAnimatedEmote);

        SubscribeLocalEvent<AnimatedEmotesComponent, AnimationFlipEmoteEvent>(OnFlip);
        SubscribeLocalEvent<AnimatedEmotesComponent, AnimationSpinEmoteEvent>(OnSpin);
        SubscribeLocalEvent<AnimatedEmotesComponent, AnimationJumpEmoteEvent>(OnJump);
    }

    // Animations writing the same sprite property share a key, so they can never fight over it.
    private const string RotationAnimationKey = "emoteAnimRotation";
    private const string OffsetAnimationKey = "emoteAnimOffset";

    public void PlayEmote(EntityUid uid, Animation anim, string animationKey)
    {
        if (_anim.HasRunningAnimation(uid, animationKey))
            return;

        _anim.Play(uid, anim, animationKey);
    }

    private void OnAnimatedEmote(AnimatedEmoteEvent ev)
    {
        if (!TryGetEntity(ev.Entity, out var uid)
        || !HasComp<AnimatedEmotesComponent>(uid)
        || !ProtoMan.TryIndex(ev.Emote, out var emote)
        || emote.Event == null)
            return;

        // Cast keeps dispatch on the runtime type, so the right animation handler is picked.
        RaiseLocalEvent(uid.Value, (object) emote.Event);
    }

    private void OnFlip(Entity<AnimatedEmotesComponent> ent, ref AnimationFlipEmoteEvent args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        // Offset from the current rotation, otherwise a downed mob ends up standing upright.
        var startRot = sprite.Rotation;
        var a = new Animation
        {
            Length = TimeSpan.FromMilliseconds(500),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Rotation),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(startRot, 0f),
                        new AnimationTrackProperty.KeyFrame(startRot + Angle.FromDegrees(180), 0.25f),
                        new AnimationTrackProperty.KeyFrame(startRot + Angle.FromDegrees(360), 0.25f),
                    }
                }
            }
        };
        PlayEmote(ent, a, RotationAnimationKey);
    }
    private void OnSpin(Entity<AnimatedEmotesComponent> ent, ref AnimationSpinEmoteEvent args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        // Mobs are noRot, so LocalRotation is dropped from the render matrix and only picks the
        // RSI direction. Spin the sprite instead, same as flip.
        var startRot = sprite.Rotation;
        var a = new Animation
        {
            Length = TimeSpan.FromMilliseconds(600),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Rotation),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(startRot, 0f),
                        new AnimationTrackProperty.KeyFrame(startRot + Angle.FromDegrees(90), 0.075f),
                        new AnimationTrackProperty.KeyFrame(startRot + Angle.FromDegrees(180), 0.075f),
                        new AnimationTrackProperty.KeyFrame(startRot + Angle.FromDegrees(270), 0.075f),
                        new AnimationTrackProperty.KeyFrame(startRot + Angle.FromDegrees(360), 0.075f),
                        new AnimationTrackProperty.KeyFrame(startRot + Angle.FromDegrees(450), 0.075f),
                        new AnimationTrackProperty.KeyFrame(startRot + Angle.FromDegrees(540), 0.075f),
                        new AnimationTrackProperty.KeyFrame(startRot + Angle.FromDegrees(630), 0.075f),
                        new AnimationTrackProperty.KeyFrame(startRot + Angle.FromDegrees(720), 0.075f),
                    }
                }
            }
        };
        PlayEmote(ent, a, RotationAnimationKey);
    }
    private void OnJump(Entity<AnimatedEmotesComponent> ent, ref AnimationJumpEmoteEvent args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        // Offset from the current offset, otherwise this cancels jittering and stun shakes.
        var startOffset = sprite.Offset;
        var a = new Animation
        {
            Length = TimeSpan.FromMilliseconds(250),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    InterpolationMode = AnimationInterpolationMode.Cubic,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(startOffset, 0f),
                        new AnimationTrackProperty.KeyFrame(startOffset + new Vector2(0, .35f), 0.125f),
                        new AnimationTrackProperty.KeyFrame(startOffset, 0.125f),
                    }
                }
            }
        };
        PlayEmote(ent, a, OffsetAnimationKey);
    }
}
