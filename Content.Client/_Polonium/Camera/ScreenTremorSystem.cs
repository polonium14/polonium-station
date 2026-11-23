using System.Numerics;
using Content.Shared._Polonium.Camera;
using Content.Shared.Camera;
using Content.Shared.CCVar;
using Content.Shared.Movement.Systems;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Camera;

/// <summary>
/// Shakes the screen like an earthquake: a slow sway with a faster trembling on top,
/// instead of the separate kicks of camera recoil.
/// </summary>
public sealed partial class ScreenTremorSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedContentEyeSystem _eye = default!;

    // Screen offset of the strongest tremor, in tiles.
    private const float MaxOffset = 0.25f;

    private const float Attack = 0.1f;
    private const float Release = 0.3f;

    // How fast the shown amplitude follows the tremors, so overlapping ones blend smoothly.
    private const float Smoothing = 8f;

    // Frequencies in hertz and weights of the waves the trembling is made of. The axes use unrelated frequencies,
    // so the screen doesn't move along a repeating figure.
    private static readonly (float Frequency, float Weight, float Phase)[] WavesX = [(0.9f, 0.35f, 0f), (4.3f, 0.4f, 1.3f), (7.9f, 0.25f, 4.1f)];
    private static readonly (float Frequency, float Weight, float Phase)[] WavesY = [(1.3f, 0.35f, 2.2f), (5.1f, 0.4f, 0.7f), (9.7f, 0.25f, 3.4f)];

    private float _intensity;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<ScreenTremorEvent>(OnTremor);
        SubscribeLocalEvent<ScreenTremorComponent, GetEyeOffsetEvent>(OnGetEyeOffset);

        Subs.CVar(_cfg, CCVars.ScreenShakeIntensity, value => _intensity = value, true);
    }

    private void OnTremor(ScreenTremorEvent ev)
    {
        if (_player.LocalEntity is not { } local || ev.Intensity <= 0f || ev.Duration <= 0f)
            return;

        EnsureComp<ScreenTremorComponent>(local).Tremors.Add(new ActiveTremor(_timing.RealTime, ev.Duration, ev.Intensity));
    }

    private void OnGetEyeOffset(Entity<ScreenTremorComponent> ent, ref GetEyeOffsetEvent args)
    {
        args.Offset += ent.Comp.Offset;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var now = _timing.RealTime;
        var time = (float) now.TotalSeconds;
        var query = EntityQueryEnumerator<ScreenTremorComponent, EyeComponent>();
        while (query.MoveNext(out var uid, out var tremor, out var eye))
        {
            var target = 0f;
            for (var i = tremor.Tremors.Count - 1; i >= 0; i--)
            {
                var t = tremor.Tremors[i];
                var age = (float) (now - t.Start).TotalSeconds;
                if (age >= t.Duration)
                {
                    tremor.Tremors.RemoveAt(i);
                    continue;
                }

                target = MathF.Max(target, t.Intensity * Envelope(age, t.Duration));
            }

            tremor.Amplitude += (target - tremor.Amplitude) * MathF.Min(1f, frameTime * Smoothing);

            if (tremor.Tremors.Count == 0 && tremor.Amplitude < 0.001f)
            {
                tremor.Offset = Vector2.Zero;
                _eye.UpdateEyeOffset((uid, eye));
                RemCompDeferred<ScreenTremorComponent>(uid);
                continue;
            }

            var shake = new Vector2(Waves(WavesX, time), Waves(WavesY, time));
            tremor.Offset = shake * MathF.Min(tremor.Amplitude, 1f) * MaxOffset * _intensity;
            _eye.UpdateEyeOffset((uid, eye));
        }
    }

    private static float Envelope(float age, float duration)
    {
        var rise = MathF.Min(1f, age / MathF.Min(Attack, duration / 3f));
        var fall = MathF.Min(1f, (duration - age) / MathF.Min(Release, duration / 2f));
        return MathF.Max(0f, MathF.Min(rise, fall));
    }

    private static float Waves((float Frequency, float Weight, float Phase)[] waves, float time)
    {
        var sum = 0f;
        foreach (var (frequency, weight, phase) in waves)
        {
            sum += weight * MathF.Sin(MathF.Tau * frequency * time + phase);
        }

        return sum;
    }
}
