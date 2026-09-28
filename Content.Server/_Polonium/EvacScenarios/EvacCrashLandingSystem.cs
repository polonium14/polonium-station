using System.Linq;
using System.Numerics;
using Content.Server.Explosion.EntitySystems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Parallax;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Shared.Camera;
using Content.Shared.GameTicking.Components;
using Content.Shared.Light.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Random.Helpers;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Polonium.EvacScenarios;

/// <summary>
/// Sends evac scenario shuttles crashing onto a planet instead of CentComm and spawns monsters around them.
/// </summary>
public sealed partial class EvacCrashLandingSystem : GameRuleSystem<EvacCrashLandingComponent>
{
    [Dependency] private BiomeSystem _biome = default!;
    [Dependency] private EvacScenarioSystem _scenario = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedCameraRecoilSystem _recoil = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;

    private const int SpawnAttempts = 10;

    private readonly List<(Vector2i Index, Tile Tile)> _reservedTiles = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EvacShuttleCourseEvent>(OnShuttleCourse);
        SubscribeLocalEvent<FTLCompletedEvent>(OnFTLCompleted);
    }

    protected override void Started(EntityUid uid, EvacCrashLandingComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        component.MobWaves.Sort((a, b) => a.Delay.CompareTo(b.Delay));

        if (component.Planets.Count == 0)
        {
            Log.Error($"{ToPrettyString(uid)} has no planets to crash on, the shuttles will fly to CentComm");
            return;
        }

        component.PlanetIndex = RobustRandom.Next(component.Planets.Count);
        var planet = component.Planets[component.PlanetIndex];

        var mapUid = _map.CreateMap(out _);
        _biome.EnsurePlanet(mapUid, ProtoMan.Index(planet.Biome), mapLight: planet.Light);
        // A crash in the middle of the night would leave nothing to see.
        RemComp<LightCycleComponent>(mapUid);
        _metaData.SetEntityName(mapUid, Loc.GetString(planet.Name));
        component.Planet = mapUid;
    }

    protected override void Ended(EntityUid uid, EvacCrashLandingComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);

        // Called off before the crash, so fly to CentComm after all.
        var stillHeading = false;
        foreach (var shuttle in component.Diverted)
        {
            if (component.Landed.Contains(shuttle)
                || !TryComp<FTLComponent>(shuttle, out var ftl)
                || ftl.State is not (FTLState.Starting or FTLState.Travelling or FTLState.Arriving))
            {
                continue;
            }

            if (FindCentcomm() is not { } centcomm)
            {
                stillHeading = true;
                continue;
            }

            ftl.TargetCoordinates = new EntityCoordinates(centcomm, Vector2.Zero);
            Dirty(shuttle, ftl);
        }

        if (component.Landed.Count == 0 && !stillHeading && component.Planet is { } planet)
        {
            QueueDel(planet);
            component.Planet = null;
        }
    }

    protected override void AppendRoundEndText(EntityUid uid, EvacCrashLandingComponent component, GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, component, gameRule, ref args);

        if (component.LandedAt == null)
            return;

        args.AddLine(Loc.GetString("evac-scenario-crash-round-end",
            ("planet", Loc.GetString(component.Planets[component.PlanetIndex].Name))));
    }

    protected override void ActiveTick(EntityUid uid, EvacCrashLandingComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (component.LandedAt is not { } landedAt)
            return;

        while (component.NextMobWave < component.MobWaves.Count
               && Timing.CurTime >= landedAt + component.MobWaves[component.NextMobWave].Delay)
        {
            var wave = component.MobWaves[component.NextMobWave];
            foreach (var shuttle in component.Landed)
            {
                SpawnMobWave(component, shuttle, wave);
            }

            component.NextMobWave++;
        }
    }

    private void OnShuttleCourse(ref EvacShuttleCourseEvent ev)
    {
        if (ev.Handled || !TryComp<ShuttleComponent>(ev.Shuttle, out var shuttle))
            return;

        var query = EntityQueryEnumerator<EvacCrashLandingComponent, EvacScenarioComponent, ActiveGameRuleComponent>();
        while (query.MoveNext(out var crash, out var scenario, out _))
        {
            if (crash.Planet is not { } planet || !Exists(planet) || !_scenario.GetShuttles(scenario).Contains(ev.Shuttle))
                continue;

            var site = new Vector2(crash.Diverted.Count * crash.LandingSpacing, 0f);

            // The crash time counts from the start of the scenario, the hyperspace time from the jump.
            _shuttle.FTLToCoordinates(ev.Shuttle,
                shuttle,
                new EntityCoordinates(planet, site),
                RobustRandom.NextAngle(),
                ev.StartupTime,
                (float) (scenario.StartDelay + crash.CrashTime).TotalSeconds);

            crash.Diverted.Add(ev.Shuttle);
            ev.Handled = true;
            return;
        }
    }

    private void OnFTLCompleted(ref FTLCompletedEvent ev)
    {
        var query = EntityQueryEnumerator<EvacCrashLandingComponent, ActiveGameRuleComponent>();
        while (query.MoveNext(out var crash, out _))
        {
            if (ev.MapUid != crash.Planet || !crash.Diverted.Contains(ev.Entity) || !crash.Landed.Add(ev.Entity))
                continue;

            crash.LandedAt ??= Timing.CurTime;
            Impact(crash, ev.Entity);
        }
    }

    private void Impact(EvacCrashLandingComponent crash, EntityUid shuttle)
    {
        var aboard = Filter.BroadcastGrid(shuttle);

        if (crash.ImpactSound != null)
            _audio.PlayGlobal(crash.ImpactSound, aboard, true);

        foreach (var session in aboard.Recipients)
        {
            if (session.AttachedEntity is { } passenger)
                _recoil.KickCamera(passenger, RobustRandom.NextAngle().ToVec() * crash.ImpactShake);
        }

        if (!TryComp<MapGridComponent>(shuttle, out var grid))
            return;

        var tiles = _map.GetAllTiles(shuttle, grid).ToList();
        for (var i = 0; i < crash.ImpactExplosions && tiles.Count > 0; i++)
        {
            var tile = RobustRandom.PickAndTake(tiles);
            _explosion.QueueExplosion(_transform.ToMapCoordinates(_map.GridTileToLocal(shuttle, grid, tile.GridIndices)),
                crash.ImpactExplosionType,
                totalIntensity: crash.ImpactExplosionIntensity,
                slope: 4f,
                maxTileIntensity: crash.ImpactExplosionMaxTileIntensity,
                cause: null,
                canCreateVacuum: false);
        }
    }

    private void SpawnMobWave(EvacCrashLandingComponent crash, EntityUid shuttle, EvacCrashMobWave wave)
    {
        if (crash.Planet is not { } planet
            || !TryComp<MapGridComponent>(planet, out var planetGrid)
            || !TryComp<BiomeComponent>(planet, out var biome)
            || !TryComp<MapGridComponent>(shuttle, out var shuttleGrid)
            || !TryComp(shuttle, out TransformComponent? shuttleXform)
            || shuttleXform.MapUid != planet)
        {
            return;
        }

        var mobs = crash.Planets[crash.PlanetIndex].Mobs;
        if (mobs.Count == 0)
            return;

        var count = Math.Clamp((int) MathF.Round(CountPassengers(shuttle) * wave.PerPassenger), wave.Min, wave.Max);
        var shuttleMatrix = _transform.GetWorldMatrix(shuttleXform);

        for (var i = 0; i < count; i++)
        {
            if (TryFindMobSpot(crash, (planet, planetGrid, biome), shuttleGrid.LocalAABB, shuttleMatrix, out var spot))
                Spawn(RobustRandom.Pick(mobs), spot);
        }
    }

    private int CountPassengers(EntityUid shuttle)
    {
        var count = 0;
        var query = EntityQueryEnumerator<ActorComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var mobState, out var xform))
        {
            if (xform.GridUid == shuttle && _mobState.IsAlive(uid, mobState))
                count++;
        }

        return count;
    }

    private bool TryFindMobSpot(
        EvacCrashLandingComponent crash,
        Entity<MapGridComponent, BiomeComponent> planet,
        Box2 hull,
        Matrix3x2 shuttleMatrix,
        out EntityCoordinates spot)
    {
        for (var attempt = 0; attempt < SpawnAttempts; attempt++)
        {
            var ring = hull.Enlarged(RobustRandom.NextFloat(crash.MobMinDistance, crash.MobMaxDistance));
            var world = Vector2.Transform(PickPointOnEdge(ring), shuttleMatrix);
            var indices = _map.WorldToTile(planet.Owner, planet.Comp1, world);

            if (_map.TryFindGridAt(new MapCoordinates(world, Transform(planet.Owner).MapID), out var gridAt, out _)
                && gridAt != planet.Owner)
            {
                continue;
            }

            // Nothing in the way, and nothing the biome is going to put there once it loads.
            if (_map.GetAnchoredEntities(planet.Owner, planet.Comp1, indices).MoveNext(out _)
                || _biome.TryGetEntity(indices, planet.Comp2, (planet.Owner, planet.Comp1), out _))
            {
                continue;
            }

            // Ground under their feet even if the biome hasn't loaded there yet.
            _reservedTiles.Clear();
            _biome.ReserveTiles(planet.Owner,
                Box2.CenteredAround(indices + new Vector2(0.5f), new Vector2(0.5f)),
                _reservedTiles,
                planet.Comp2,
                planet.Comp1);

            spot = _map.GridTileToLocal(planet.Owner, planet.Comp1, indices);
            return true;
        }

        spot = default;
        return false;
    }

    private Vector2 PickPointOnEdge(Box2 box)
    {
        var along = RobustRandom.NextFloat(0f, 2f * (box.Width + box.Height));

        if (along < box.Width)
            return new Vector2(box.Left + along, box.Bottom);

        along -= box.Width;
        if (along < box.Height)
            return new Vector2(box.Right, box.Bottom + along);

        along -= box.Height;
        if (along < box.Width)
            return new Vector2(box.Right - along, box.Top);

        along -= box.Width;
        return new Vector2(box.Left, box.Top - along);
    }

    private EntityUid? FindCentcomm()
    {
        var query = EntityQueryEnumerator<StationCentcommComponent>();
        while (query.MoveNext(out var centcomm))
        {
            if (Exists(centcomm.Entity))
                return centcomm.Entity;
        }

        return null;
    }
}
