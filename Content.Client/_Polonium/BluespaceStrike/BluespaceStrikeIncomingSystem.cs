// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._Polonium.BluespaceStrike;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;

namespace Content.Client._Polonium.BluespaceStrike;

/// <summary>
/// Animates the incoming bluespace projectile falling from above onto the strike epicenter.
/// </summary>
public sealed partial class BluespaceStrikeIncomingSystem : EntitySystem
{
    [Dependency] private AnimationPlayerSystem _anim = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private const string AnimationKey = "bluespace_strike_fall";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BluespaceStrikeIncomingComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<BluespaceStrikeIncomingComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp(ent, out SpriteComponent? sprite))
            return;

        var animPlayer = EnsureComp<AnimationPlayerComponent>(ent);
        if (_anim.HasRunningAnimation(animPlayer, AnimationKey))
            return;

        var duration = (float)ent.Comp.FallDuration.TotalSeconds;
        if (duration <= 0f)
            duration = BluespaceStrikeComponent.FallDurationSeconds;

        var startOffset = new Vector2(0f, ent.Comp.StartOffsetY);
        _sprite.SetOffset((ent.Owner, sprite), startOffset);

        var animation = new Animation
        {
            Length = TimeSpan.FromSeconds(duration),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(startOffset, 0f),
                        new AnimationTrackProperty.KeyFrame(Vector2.Zero, duration),
                    },
                },
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Scale),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(new Vector2(5f, 5f), 0f),
                        new AnimationTrackProperty.KeyFrame(new Vector2(2.5f, 2.5f), duration),
                    },
                },
            },
        };

        _anim.Play((ent, animPlayer), animation, AnimationKey);
    }
}
