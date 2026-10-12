using Content.Shared._Polonium.Replicator;
using Robust.Client.Animations;
using Robust.Client.GameObjects;

namespace Content.Client._Polonium.Replicator;

public sealed partial class ReplicatorColossusBeaconVisualsSystem : VisualizerSystem<ReplicatorColossusBeaconComponent>
{
    [Dependency] private AnimationPlayerSystem _animation = default!;

    private const string OpenKey = "replicator-beacon-open";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorColossusBeaconComponent, AnimationCompletedEvent>(OnAnimationCompleted);
    }

    protected override void OnAppearanceChange(EntityUid uid, ReplicatorColossusBeaconComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        AppearanceSystem.TryGetData<int>(uid, ReplicatorColossusBeaconVisuals.Stage, out var stage, args.Component);
        AppearanceSystem.TryGetData<ReplicatorColossusBeaconState>(uid, ReplicatorColossusBeaconVisuals.State, out var state, args.Component);

        var sprite = (uid, args.Sprite);
        SpriteSystem.LayerSetRsiState(sprite, ReplicatorColossusBeaconVisualLayers.Orb, state switch
        {
            ReplicatorColossusBeaconState.Firing => "firing",
            ReplicatorColossusBeaconState.Ready => "ready",
            _ => $"orb_{stage}",
        });

        var open = state != ReplicatorColossusBeaconState.Charging;
        var opening = open && component.Open == false;
        component.Open = open;

        if (opening)
        {
            _animation.Play(uid, GetOpenAnimation(component.OpenTime), OpenKey);
            return;
        }

        if (!_animation.HasRunningAnimation(uid, OpenKey))
            SetClaws(sprite, state);
    }

    private void OnAnimationCompleted(Entity<ReplicatorColossusBeaconComponent> ent, ref AnimationCompletedEvent args)
    {
        if (args.Key != OpenKey || !TryComp<SpriteComponent>(ent, out var sprite))
            return;

        AppearanceSystem.TryGetData<ReplicatorColossusBeaconState>(ent, ReplicatorColossusBeaconVisuals.State, out var state);
        SetClaws((ent.Owner, sprite), state);
    }

    private void SetClaws(Entity<SpriteComponent?> sprite, ReplicatorColossusBeaconState state)
    {
        var (claws, lights) = state switch
        {
            ReplicatorColossusBeaconState.Firing => ("claws_open", "lights_firing"),
            ReplicatorColossusBeaconState.Ready => ("claws_open", "lights_ready"),
            _ => ("claws", "lights"),
        };

        SpriteSystem.LayerSetRsiState(sprite, ReplicatorColossusBeaconVisualLayers.Claws, claws);
        SpriteSystem.LayerSetRsiState(sprite, ReplicatorColossusBeaconVisualLayers.Lights, lights);
    }

    private static Animation GetOpenAnimation(TimeSpan length)
    {
        return new Animation
        {
            Length = length,
            AnimationTracks =
            {
                new AnimationTrackSpriteFlick
                {
                    LayerKey = ReplicatorColossusBeaconVisualLayers.Claws,
                    KeyFrames = { new AnimationTrackSpriteFlick.KeyFrame("claws_opening", 0f) },
                },
                new AnimationTrackSpriteFlick
                {
                    LayerKey = ReplicatorColossusBeaconVisualLayers.Lights,
                    KeyFrames = { new AnimationTrackSpriteFlick.KeyFrame("lights_opening", 0f) },
                },
            },
        };
    }
}
