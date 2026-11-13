using Content.Shared._Polonium.Power.Generation.FissionGenerator;
using Robust.Client.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Power.Generation.FissionGenerator;

/// <summary>
/// Makes a reactor glow brighter, pulse faster and drone louder as it approaches a meltdown, then lets the glow
/// of its ruins and the smoke column above them fade down to a residual level.
/// </summary>
public sealed partial class ReactorMeltdownVisualsSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPointLightSystem _lights = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    [Dependency] private EntityQuery<SpriteComponent> _spriteQuery = default!;

    // How fast the shown danger catches up with the networked one, which changes in steps.
    private const float DangerCatchUp = 2f;

    // Two waves with unrelated rates, so the residual glow wavers instead of pulsing evenly.
    private const float SlowFlickerRate = 1.3f;
    private const float FastFlickerRate = 3.1f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReactorMeltdownVisualsComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<ReactorMeltdownVisualsComponent> ent, ref ComponentShutdown args)
    {
        RemoveLight(ent.Comp);
        StopBuildUpSound(ent.Comp);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ReactorMeltdownVisualsComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.MeltdownTime is { } meltdown)
                UpdateAfterglow((uid, comp), (float) (now - meltdown).TotalSeconds);
            else
                UpdateBuildUp((uid, comp), frameTime);
        }
    }

    private void UpdateBuildUp(Entity<ReactorMeltdownVisualsComponent> ent, float frameTime)
    {
        var comp = ent.Comp;
        comp.ShownDanger += (comp.Danger - comp.ShownDanger) * MathF.Min(1f, frameTime * DangerCatchUp);

        if (comp.Danger <= 0f && comp.ShownDanger < 0.01f)
        {
            comp.ShownDanger = 0f;
            RemoveLight(comp);
            StopBuildUpSound(comp);
            return;
        }

        var frequency = MathHelper.Lerp(comp.BuildUpPulseFrequency.X, comp.BuildUpPulseFrequency.Y, comp.ShownDanger);
        comp.PulsePhase = (comp.PulsePhase + frameTime * frequency) % 1f;

        var pulse = 0.5f + 0.5f * MathF.Sin(comp.PulsePhase * MathF.Tau);
        var energy = comp.BuildUpGlowEnergy * comp.ShownDanger * (0.6f + 0.4f * pulse);
        var radius = MathHelper.Lerp(comp.BuildUpMinGlowRadius, comp.BuildUpMaxGlowRadius, comp.ShownDanger);
        SetLight(ent, energy, radius);
        SetBuildUpSound(ent);
    }

    private void UpdateAfterglow(Entity<ReactorMeltdownVisualsComponent> ent, float age)
    {
        var comp = ent.Comp;
        StopBuildUpSound(comp);
        var heat = MathF.Exp(-age / (float) comp.GlowFadeTime.TotalSeconds);

        var energy = MathHelper.Lerp(comp.ResidualGlowEnergy, comp.MeltdownGlowEnergy, heat);
        var radius = MathHelper.Lerp(comp.ResidualGlowRadius, comp.MeltdownGlowRadius, heat);
        var flicker = 0.6f * MathF.Sin(age * SlowFlickerRate) + 0.4f * MathF.Sin(age * FastFlickerRate + 1f);
        SetLight(ent, energy * (1f + comp.GlowFlicker * flicker), radius);

        if (comp.SmokeColumn is { } column && _spriteQuery.TryComp(column, out var sprite))
            _sprite.SetColor((column, sprite), Color.White.WithAlpha(MathHelper.Lerp(comp.ResidualSmokeAlpha, 1f, heat)));
    }

    private void SetLight(Entity<ReactorMeltdownVisualsComponent> ent, float energy, float radius)
    {
        ent.Comp.LightEntity ??= SpawnAttachedTo(null, new EntityCoordinates(ent, default));

        var lightUid = ent.Comp.LightEntity.Value;
        var light = EnsureComp<PointLightComponent>(lightUid);
        _lights.SetColor(lightUid, ent.Comp.GlowColor, light);
        _lights.SetEnergy(lightUid, energy, light);
        _lights.SetRadius(lightUid, radius, light);
    }

    private void SetBuildUpSound(Entity<ReactorMeltdownVisualsComponent> ent)
    {
        if (ent.Comp.BuildUpSound is not { } sound)
            return;

        var volume = sound.Params.Volume + SharedAudioSystem.GainToVolume(ent.Comp.ShownDanger);

        if (Exists(ent.Comp.BuildUpStream))
        {
            _audio.SetVolume(ent.Comp.BuildUpStream, volume);
            return;
        }

        var stream = _audio.PlayEntity(sound, Filter.Local(), ent, false, sound.Params.WithLoop(true).WithVolume(volume));
        ent.Comp.BuildUpStream = stream?.Entity;
    }

    private void StopBuildUpSound(ReactorMeltdownVisualsComponent comp)
    {
        if (comp.BuildUpStream == null)
            return;

        comp.BuildUpStream = _audio.Stop(comp.BuildUpStream);
    }

    private void RemoveLight(ReactorMeltdownVisualsComponent comp)
    {
        if (comp.LightEntity is not { } light)
            return;

        if (!TerminatingOrDeleted(light))
            Del(light);

        comp.LightEntity = null;
    }
}
