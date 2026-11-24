using System.Numerics;
using Content.Shared._Polonium.Medical.Cpr;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;

namespace Content.Client._Polonium.Medical.Cpr;

/// <summary>
/// Plays the short downward push of a rescuer on each cycle of chest compressions. Ported from Funky Station
/// (forky-station PR #325).
/// </summary>
public sealed partial class CprSystem : EntitySystem
{
    [Dependency] private AnimationPlayerSystem _animation = default!;

    private const string LungeKey = "cpr-lunge";

    private static readonly Animation Lunge = new()
    {
        Length = TimeSpan.FromSeconds(1),
        AnimationTracks =
        {
            new AnimationTrackComponentProperty
            {
                ComponentType = typeof(SpriteComponent),
                Property = nameof(SpriteComponent.Offset),
                InterpolationMode = AnimationInterpolationMode.Cubic,
                KeyFrames =
                {
                    new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0f),
                    new AnimationTrackProperty.KeyFrame(new Vector2(0f, -0.12f), 0.04f),
                    new AnimationTrackProperty.KeyFrame(new Vector2(0f, -0.16f), 0.08f),
                    new AnimationTrackProperty.KeyFrame(Vector2.Zero, 0.2f),
                },
            },
        },
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<CprLungeEvent>(OnLunge);
    }

    private void OnLunge(CprLungeEvent args)
    {
        var rescuer = GetEntity(args.Rescuer);
        if (!Exists(rescuer))
            return;

        _animation.Stop(rescuer, LungeKey);
        _animation.Play(rescuer, Lunge, LungeKey);
    }
}
