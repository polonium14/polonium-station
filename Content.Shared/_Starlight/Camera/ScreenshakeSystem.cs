using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Noise;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Camera;

/// <summary>
/// Shakes the local player's camera. Purely visual, the shake is never networked.
/// </summary>
public sealed partial class ScreenshakeSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private SharedEyeSystem _eye = default!;

    private const float MaxAngleDegrees = 20f;

    private readonly List<(ScreenshakeParameters Parameters, TimeSpan Start)> _rotational = [];
    private readonly FastNoiseLite _noise = new(67 + 420);
    private float _intensity;

    public override void Initialize()
    {
        base.Initialize();

        // The eye lerping resets the rotation every frame, the shake goes on top of it.
        UpdatesAfter.Add(typeof(SharedEyeSystem));

        _noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);

        if (_net.IsClient)
            Subs.CVar(_cfg, CCVars.ScreenShakeIntensity, value => _intensity = value, true);
    }

    public void Screenshake(EntityUid uid, ScreenshakeParameters rotation)
    {
        if (uid != _player.LocalEntity || _intensity <= 0f)
            return;

        _rotational.Add((rotation, _timing.RealTime));
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_rotational.Count == 0)
            return;

        if (_player.LocalEntity is not { } uid || !TryComp<EyeComponent>(uid, out var eye))
        {
            _rotational.Clear();
            return;
        }

        var now = _timing.RealTime;
        var degrees = 0f;
        for (var i = _rotational.Count - 1; i >= 0; i--)
        {
            var (parameters, start) = _rotational[i];
            var seconds = (float) (now - start).TotalSeconds;

            // Trauma decays quadratically with the time passed.
            var trauma = parameters.Trauma - seconds * seconds * parameters.DecayRate;
            if (trauma <= 0f)
            {
                _rotational.RemoveAt(i);
                continue;
            }

            _noise.SetFrequency(parameters.Frequency);
            degrees += MaxAngleDegrees * trauma * _intensity
                       * _noise.GetNoise((float) now.TotalMilliseconds, (float) start.TotalMilliseconds);
        }

        _eye.SetRotation(uid, eye.Rotation + Angle.FromDegrees(degrees), eye);
    }
}

public readonly record struct ScreenshakeParameters(float Trauma, float DecayRate, float Frequency);
