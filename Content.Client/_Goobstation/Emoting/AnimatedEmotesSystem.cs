// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

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

    // One key per sprite property an emote writes, so emotes touching different properties can overlap.
    private const string RotationAnimationKey = "emoteAnimRotation";
    private const string OffsetAnimationKey = "emoteAnimOffset";

    // Spin cycles the sprite's facing rather than rotating it, so it can't use the animation player.
    private static readonly Direction[] SpinDirections =
    {
        Direction.West,
        Direction.North,
        Direction.East,
        Direction.South,
    };

    private const int SpinSteps = 8;
    private const float SpinStepTime = 0.075f;

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
        // Mobs are noRot, so rotating them draws nothing. Step the sprite through its four facings
        // instead, which is what a spin is supposed to look like.
        if (!TryComp<SpriteComponent>(ent, out var sprite) || HasComp<SpinningEmoteComponent>(ent))
            return;

        var spin = AddComp<SpinningEmoteComponent>(ent);
        spin.PreviousDirection = sprite.DirectionOverride;
        spin.PreviousEnabled = sprite.EnableDirectionOverride;

        sprite.DirectionOverride = SpinDirections[0];
        sprite.EnableDirectionOverride = true;
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<SpinningEmoteComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var spin, out var sprite))
        {
            spin.Accumulator += frameTime;
            if (spin.Accumulator < SpinStepTime)
                continue;

            spin.Accumulator -= SpinStepTime;
            spin.Step++;

            if (spin.Step >= SpinSteps)
            {
                sprite.DirectionOverride = spin.PreviousDirection;
                sprite.EnableDirectionOverride = spin.PreviousEnabled;
                RemCompDeferred<SpinningEmoteComponent>(uid);
                continue;
            }

            sprite.DirectionOverride = SpinDirections[spin.Step % SpinDirections.Length];
        }
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
