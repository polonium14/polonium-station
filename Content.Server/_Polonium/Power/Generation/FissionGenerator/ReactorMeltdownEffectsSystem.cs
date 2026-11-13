using Content.Shared._FarHorizons.Power.Generation.FissionGenerator;
using Content.Shared._Polonium.Camera;
using Content.Shared._Polonium.Power.Generation.FissionGenerator;
using Content.Shared.Explosion;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Physics;
using Content.Shared.Station.Components;
using Content.Shared.Station.Systems;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Power.Generation.FissionGenerator;

public sealed partial class ReactorMeltdownEffectsSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPoweredLightSystem _poweredLight = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private StationSystem _station = default!;

    [Dependency] private EntityQuery<BlinkingPoweredLightComponent> _blinkingQuery = default!;
    [Dependency] private EntityQuery<CoriumComponent> _coriumQuery = default!;
    [Dependency] private EntityQuery<PhysicsComponent> _physicsQuery = default!;
    [Dependency] private EntityQuery<ReactorMeltdownVisualsComponent> _visualsQuery = default!;

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.1);

    // The glow only changes in steps of this size, so the reactor isn't dirtied on every update.
    private const float DangerStep = 0.05f;

    // Same as for explosion shockwaves: players see the wave a bit beyond its radius.
    private const float ShockwaveViewMargin = 18f;

    private const int BlockingLayers = (int) (CollisionGroup.Impassable | CollisionGroup.HighImpassable);

    // How far the effects get by the meltdown temperature. The critical phase above it brings them to full strength.
    private const float BuildUpShare = 0.5f;

    // How strongly the start of the build-up is felt, compared with its end right before the meltdown.
    private const float BuildUpStartStrength = 0.5f;

    // Gain of the earthquake rumble at the start of the build-up, compared with its end.
    private const float BuildUpStartRumbleGain = 0.25f;

    // The constant trembling is sent this often, each part lasting two intervals so they overlap.
    private static readonly TimeSpan RumbleShakeInterval = TimeSpan.FromSeconds(0.5);

    private static readonly Vector2i[] Neighbours = [new(1, 0), new(-1, 0), new(0, 1), new(0, -1)];

    private readonly HashSet<Entity<PoweredLightComponent>> _nearbyLights = new();
    private readonly HashSet<Vector2i> _footprint = new();
    private readonly List<Vector2i> _candidates = new();
    private readonly HashSet<ICommonSession> _rumbling = new();
    private readonly List<ICommonSession> _stopRumbling = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReactorMeltdownEffectsComponent, ReactorMeltdownAttemptEvent>(OnMeltdownAttempt);
        SubscribeLocalEvent<ReactorMeltdownEffectsComponent, ReactorMeltdownEvent>(OnMeltdown);
        SubscribeLocalEvent<ReactorMeltdownEffectsComponent, ComponentShutdown>(OnEffectsShutdown);
        SubscribeLocalEvent<ReactorMeltdownVisualsComponent, ComponentShutdown>(OnVisualsShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ReactorMeltdownEffectsComponent, NuclearReactorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var effects, out var reactor, out var xform))
        {
            if (now < effects.NextUpdate)
                continue;

            effects.NextUpdate = now + UpdateInterval;
            var ent = new Entity<ReactorMeltdownEffectsComponent, TransformComponent>(uid, effects, xform);

            if (!reactor.Melted)
            {
                UpdateBuildUp(ent, reactor, now);
            }
            else
            {
                StopRumble(effects);

                if (_visualsQuery.TryComp(uid, out var visuals) && visuals.MeltdownTime != null)
                    UpdateAftermath(ent, now);
            }
        }
    }

    private void OnMeltdownAttempt(Entity<ReactorMeltdownEffectsComponent> ent, ref ReactorMeltdownAttemptEvent args)
    {
        var now = _timing.CurTime;

        if (ent.Comp.CriticalStart == null)
        {
            ent.Comp.CriticalStart = now;
            ent.Comp.NextTremor = now;
        }

        if (now < ent.Comp.CriticalStart + ent.Comp.CriticalDuration)
            args.Cancelled = true;
    }

    private void OnMeltdown(Entity<ReactorMeltdownEffectsComponent> ent, ref ReactorMeltdownEvent args)
    {
        var now = _timing.CurTime;
        var xform = Transform(ent);
        var full = new Entity<ReactorMeltdownEffectsComponent, TransformComponent>(ent, ent.Comp, xform);

        ent.Comp.BuildingUp = false;
        ent.Comp.CriticalStart = null;
        CollectStationLights(full);
        Tremor(full, ent.Comp.ExplosionSound, ent.Comp.MeltdownShake, ent.Comp.MeltdownTremorDuration, 1f);
        FlickerLights(ent.Comp, ent.Comp.MeltdownFlickerChance, ent.Comp.MeltdownFlickerDuration);
        BurstBulbs(ent.Comp, xform);
        SendShockwave(full, ent.Comp.ShockwaveRadius, ent.Comp.ShockwaveDuration, ent.Comp.ShockwaveIntensity);

        ent.Comp.NextPulse = now + _random.Next(ent.Comp.MinPulseInterval, ent.Comp.MaxPulseInterval);
        ent.Comp.NextCorium = now + ent.Comp.FirstCoriumDelay;

        if (!_visualsQuery.TryComp(ent, out var visuals))
            return;

        visuals.Danger = 1f;
        visuals.MeltdownTime = now;
        visuals.SmokeColumn = Spawn(ent.Comp.SmokeColumnPrototype, xform.Coordinates);
        Dirty(ent, visuals);
    }

    private void OnEffectsShutdown(Entity<ReactorMeltdownEffectsComponent> ent, ref ComponentShutdown args)
    {
        StopRumble(ent.Comp);
    }

    private void OnVisualsShutdown(Entity<ReactorMeltdownVisualsComponent> ent, ref ComponentShutdown args)
    {
        QueueDel(ent.Comp.SmokeColumn);
    }

    private void UpdateBuildUp(Entity<ReactorMeltdownEffectsComponent, TransformComponent> ent, NuclearReactorComponent reactor, TimeSpan now)
    {
        var effects = ent.Comp1;

        // The build-up starts together with the reactor's station-wide meltdown warning.
        var start = (reactor.ReactorFireTemp + reactor.ReactorMeltdownTemp) / 2f;
        var danger = ent.Comp2.Anchored
            ? Math.Clamp((reactor.Temperature - start) / (reactor.ReactorMeltdownTemp - start), 0f, 1f)
            : 0f;

        if (!ent.Comp2.Anchored || reactor.Temperature <= reactor.ReactorMeltdownTemp)
            effects.CriticalStart = null;

        var critical = effects.CriticalStart is { } criticalStart
            ? Math.Clamp((float) ((now - criticalStart) / effects.CriticalDuration), 0f, 1f)
            : 0f;

        var progress = BuildUpShare * danger + (1f - BuildUpShare) * critical;
        SetDanger(ent, progress);

        if (progress <= 0f)
        {
            effects.BuildingUp = false;
            StopRumble(effects);
            return;
        }

        if (!effects.BuildingUp)
        {
            effects.BuildingUp = true;
            effects.NextTremor = now;
            CollectStationLights(ent);
        }

        var strength = MathHelper.Lerp(BuildUpStartStrength, 1f, progress);
        Rumble(ent, now, strength, MathHelper.Lerp(BuildUpStartRumbleGain, 1f, progress));

        if (now < effects.NextTremor)
            return;

        Tremor(ent, effects.TremorSound, effects.TremorShake * strength, effects.TremorDuration, strength);
        _audio.PlayPvs(effects.CreakSound, ent.Owner);
        FlickerLights(effects, effects.TremorFlickerChance * strength, effects.TremorFlickerDuration);
        effects.NextTremor = now + TremorInterval(effects, progress);
    }

    private void UpdateAftermath(Entity<ReactorMeltdownEffectsComponent, TransformComponent> ent, TimeSpan now)
    {
        var effects = ent.Comp1;

        if (now >= effects.NextPulse)
        {
            SendShockwave(ent, effects.PulseRadius, effects.PulseDuration, effects.PulseIntensity);
            _audio.PlayPvs(effects.PulseSound, ent.Owner);
            effects.NextPulse = now + _random.Next(effects.MinPulseInterval, effects.MaxPulseInterval);
        }

        if (effects.CoriumSpawned >= effects.MaxCorium || now < effects.NextCorium)
            return;

        effects.NextCorium = now + _random.Next(effects.MinCoriumInterval, effects.MaxCoriumInterval);

        var count = effects.CoriumSpawned == 0 ? effects.InitialCorium : 1;
        for (var i = 0; i < count && effects.CoriumSpawned < effects.MaxCorium; i++)
        {
            if (!TrySpreadCorium(ent))
                break;
        }
    }

    private void SetDanger(EntityUid uid, float danger)
    {
        if (!_visualsQuery.TryComp(uid, out var visuals))
            return;

        var stepped = MathF.Round(danger / DangerStep) * DangerStep;
        if (MathHelper.CloseTo(visuals.Danger, stepped))
            return;

        visuals.Danger = stepped;
        Dirty(uid, visuals);
    }

    private TimeSpan TremorInterval(ReactorMeltdownEffectsComponent effects, float progress)
    {
        var seconds = MathHelper.Lerp(effects.MaxTremorInterval.TotalSeconds, effects.MinTremorInterval.TotalSeconds, progress);
        return TimeSpan.FromSeconds(seconds * _random.NextDouble(0.75, 1.25));
    }

    /// <summary>
    /// Shakes the screen of every player who feels the reactor and plays them the sound, both weaker further away.
    /// </summary>
    private void Tremor(Entity<ReactorMeltdownEffectsComponent, TransformComponent> ent, SoundSpecifier? sound, float shake, TimeSpan duration, float loudness)
    {
        var effects = ent.Comp1;
        var resolved = sound == null ? null : _audio.ResolveSound(sound);
        var epicenter = _transform.GetMapCoordinates(ent.Owner, ent.Comp2);
        var station = _station.GetOwningStation(ent.Owner, ent.Comp2);

        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity is not { } player)
                continue;

            var felt = GetStrength(player, epicenter, station, effects.ShakeRange, effects.DistantShakeFraction);
            if (felt <= 0f)
                continue;

            RaiseNetworkEvent(new ScreenTremorEvent(shake * felt, (float) duration.TotalSeconds), session);

            if (sound != null && resolved != null)
                _audio.PlayGlobal(resolved, session, sound.Params.AddVolume(SharedAudioSystem.GainToVolume(felt * loudness) + effects.StationSoundVolume));
        }
    }

    /// <summary>
    /// Shakes every player who feels the build-up and plays them the earthquake rumble, both stronger closer to the reactor.
    /// </summary>
    private void Rumble(Entity<ReactorMeltdownEffectsComponent, TransformComponent> ent, TimeSpan now, float strength, float gain)
    {
        var effects = ent.Comp1;
        var epicenter = _transform.GetMapCoordinates(ent.Owner, ent.Comp2);
        var station = _station.GetOwningStation(ent.Owner, ent.Comp2);
        var shake = now >= effects.NextRumbleShake;
        _rumbling.Clear();

        if (shake)
            effects.NextRumbleShake = now + RumbleShakeInterval;

        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity is not { } player)
                continue;

            var felt = GetStrength(player, epicenter, station, effects.ShakeRange, effects.DistantShakeFraction);
            if (felt <= 0f)
                continue;

            if (shake)
                RaiseNetworkEvent(new ScreenTremorEvent(effects.RumbleShake * strength * felt, (float) (RumbleShakeInterval * 2).TotalSeconds), session);

            if (effects.RumbleSound is not { } sound)
                continue;

            _rumbling.Add(session);

            // Whole decibels, so the stream isn't dirtied on every update.
            var volume = MathF.Round(sound.Params.Volume + SharedAudioSystem.GainToVolume(gain * felt) + effects.StationSoundVolume);

            if (effects.RumbleStreams.TryGetValue(session, out var stream) && Exists(stream))
                _audio.SetVolume(stream, volume);
            else if (_audio.PlayGlobal(sound, session, sound.Params.WithLoop(true).WithVolume(volume)) is { } started)
                effects.RumbleStreams[session] = started.Entity;
        }

        _stopRumbling.Clear();
        foreach (var session in effects.RumbleStreams.Keys)
        {
            if (!_rumbling.Contains(session))
                _stopRumbling.Add(session);
        }

        foreach (var session in _stopRumbling)
        {
            _audio.Stop(effects.RumbleStreams[session]);
            effects.RumbleStreams.Remove(session);
        }
    }

    private void StopRumble(ReactorMeltdownEffectsComponent effects)
    {
        foreach (var stream in effects.RumbleStreams.Values)
        {
            _audio.Stop(stream);
        }

        effects.RumbleStreams.Clear();
    }

    /// <summary>
    /// How strongly a player feels the reactor: weaker with distance until <paramref name="range"/>,
    /// but never weaker than <paramref name="distantFraction"/> anywhere on the reactor's station.
    /// </summary>
    private float GetStrength(EntityUid player, MapCoordinates epicenter, EntityUid? station, float range, float distantFraction)
    {
        var playerXform = Transform(player);
        if (playerXform.MapID != epicenter.MapId)
            return 0f;

        var distance = (_transform.GetWorldPosition(playerXform) - epicenter.Position).Length();
        var strength = 1f - distance / range;

        if (strength <= 0f && (station == null || _station.GetOwningStation(player, playerXform) != station))
            return 0f;

        return MathF.Max(strength, distantFraction);
    }

    private void CollectStationLights(Entity<ReactorMeltdownEffectsComponent, TransformComponent> ent)
    {
        var lights = ent.Comp1.StationLights;
        lights.Clear();

        HashSet<EntityUid>? grids = null;
        if (_station.GetOwningStation(ent.Owner, ent.Comp2) is { } station && TryComp<StationDataComponent>(station, out var data))
            grids = data.Grids;

        var query = EntityQueryEnumerator<PoweredLightComponent, TransformComponent>();
        while (query.MoveNext(out var light, out _, out var xform))
        {
            if (xform.GridUid is not { } grid)
                continue;

            if (grids?.Contains(grid) ?? grid == ent.Comp2.GridUid)
                lights.Add(light);
        }
    }

    private void FlickerLights(ReactorMeltdownEffectsComponent effects, float chance, TimeSpan duration)
    {
        var now = _timing.CurTime;

        foreach (var light in effects.StationLights)
        {
            if (!_random.Prob(chance) || TerminatingOrDeleted(light))
                continue;

            var stop = now + duration * _random.NextDouble(0.5, 1);

            // Lights that blink forever or for longer already are left alone.
            if (_blinkingQuery.TryComp(light, out var blinking) && (blinking.StopBlinkingTime is not { } current || current >= stop))
                continue;

            blinking = EnsureComp<BlinkingPoweredLightComponent>(light);
            blinking.StopBlinkingTime = stop;
            Dirty(light, blinking);
        }
    }

    private void BurstBulbs(ReactorMeltdownEffectsComponent effects, TransformComponent xform)
    {
        _nearbyLights.Clear();
        _lookup.GetEntitiesInRange(xform.Coordinates, effects.BulbBurstRange, _nearbyLights, LookupFlags.StaticSundries);

        foreach (var light in _nearbyLights)
        {
            if (_random.Prob(effects.BulbBurstChance))
                _poweredLight.TryDestroyBulb(light, light.Comp);
        }
    }

    private void SendShockwave(Entity<ReactorMeltdownEffectsComponent, TransformComponent> ent, float radius, float duration, float intensity)
    {
        var epicenter = _transform.GetMapCoordinates(ent.Owner, ent.Comp2);
        var ev = new ExplosionShockwaveEvent(epicenter.MapId, epicenter.Position, _timing.CurTime.TotalSeconds, radius, duration, intensity);
        var filter = Filter.Empty().AddInRange(epicenter, radius + ShockwaveViewMargin, _player, EntityManager);

        RaiseNetworkEvent(ev, filter);
    }

    /// <summary>
    /// Spawns one more corium tile next to the reactor or next to corium that is still molten.
    /// </summary>
    /// <returns>False if the corium has nowhere left to flow.</returns>
    private bool TrySpreadCorium(Entity<ReactorMeltdownEffectsComponent, TransformComponent> ent)
    {
        var (uid, effects, xform) = ent;

        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        _footprint.Clear();
        var worldBox = _lookup.GetWorldAABB(uid, xform);
        var localBox = _transform.GetInvWorldMatrix(gridUid).TransformBox(worldBox).Enlarged(-0.1f);
        var tiles = _map.GetLocalTilesIntersecting(gridUid, grid, localBox, ignoreEmpty: false);
        while (tiles.MoveNext(out var tile))
        {
            _footprint.Add(tile.GridIndices);
        }

        _candidates.Clear();
        effects.Corium.RemoveAll(corium => TerminatingOrDeleted(corium));

        foreach (var tile in _footprint)
        {
            AddCandidates((gridUid, grid), tile);
        }

        foreach (var corium in effects.Corium)
        {
            if (_coriumQuery.TryComp(corium, out var comp) && comp.Stage == CoriumStage.Molten)
                AddCandidates((gridUid, grid), _map.TileIndicesFor(gridUid, grid, Transform(corium).Coordinates));
        }

        if (_candidates.Count == 0)
            return false;

        var target = _random.Pick(_candidates);
        effects.Corium.Add(Spawn(effects.CoriumPrototype, _map.GridTileToLocal(gridUid, grid, target)));
        effects.CoriumSpawned++;
        return true;
    }

    private void AddCandidates(Entity<MapGridComponent> grid, Vector2i tile)
    {
        foreach (var offset in Neighbours)
        {
            var next = tile + offset;
            if (_footprint.Contains(next) || _candidates.Contains(next) || !CanHoldCorium(grid, next))
                continue;

            _candidates.Add(next);
        }
    }

    private bool CanHoldCorium(Entity<MapGridComponent> grid, Vector2i tile)
    {
        if (!_map.TryGetTileRef(grid, grid.Comp, tile, out var tileRef) || tileRef.Tile.IsEmpty)
            return false;

        var anchored = _map.GetAnchoredEntitiesEnumerator(grid, grid.Comp, tile);
        while (anchored.MoveNext(out var anchoredUid))
        {
            if (_coriumQuery.HasComp(anchoredUid))
                return false;

            if (_physicsQuery.TryComp(anchoredUid, out var body)
                && body.CanCollide
                && body.Hard
                && (body.CollisionLayer & BlockingLayers) != 0)
                return false;
        }

        return true;
    }
}
