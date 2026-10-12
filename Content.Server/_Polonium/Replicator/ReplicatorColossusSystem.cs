using System.Numerics;
using Content.Server.Atmos.Components;
using Content.Server.Chat.Systems;
using Content.Server.Destructible;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Pinpointer;
using Content.Shared._Impstation.Replicator;
using Content.Shared._Polonium.Replicator;
using Content.Shared.AlertLevel;
using Content.Shared.Camera;
using Content.Shared.DoAfter;
using Content.Shared.Ghost;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Station;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Polonium.Replicator;

public sealed partial class ReplicatorColossusSystem : EntitySystem
{
    private const CollisionGroup ObstacleLayers = CollisionGroup.Impassable | CollisionGroup.MidImpassable
                                                  | CollisionGroup.HighImpassable | CollisionGroup.LowImpassable;

    private const float LaunchKickRange = 12f;

    private static readonly Direction[] CardinalDirections = [Direction.North, Direction.South, Direction.East, Direction.West];

    [Dependency] private ITileDefinitionManager _tileDefinitions = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private AlertLevelSystem _alertLevel = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedCameraRecoilSystem _cameraRecoil = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private ReplicatorConstructionSystem _construction = default!;
    [Dependency] private DestructibleSystem _destructible = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private NavMapSystem _navMap = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private ReplicatorRecallImmunitySystem _recallImmunity = default!;
    [Dependency] private SharedReplicatorSystem _replicator = default!;
    [Dependency] private SharedStationSystem _station = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private TileSystem _tile = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;

    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();
    private readonly HashSet<Entity<CameraRecoilComponent>> _cameras = new();
    private readonly List<EntityUid> _anchored = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorColossusComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ReplicatorColossusComponent, GhostRoleSpawnerUsedEvent>(OnSpawnerUsed);
        SubscribeLocalEvent<ReplicatorColossusComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<ReplicatorColossusComponent, MobStateChangedEvent>(OnMobStateChanged);

        InitializeAbilities();
        InitializeAssimilation();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ReplicatorColossusLaunchComponent, ReplicatorColossusComponent>();
        while (query.MoveNext(out var uid, out var launch, out var colossus))
        {
            if (now >= launch.EndTime)
                FinishLaunch((uid, colossus));
        }

        UpdateAbilities(now);
        UpdateAssimilation(now);
    }

    private void OnMapInit(Entity<ReplicatorColossusComponent> ent, ref MapInitEvent args)
    {
        // Destruction thresholds merge down the prototype chain, so the regular replicators' one cannot be cleared there.
        RemComp<DestructibleComponent>(ent);
    }

    private void OnSpawnerUsed(Entity<ReplicatorColossusComponent> ent, ref GhostRoleSpawnerUsedEvent args)
    {
        if (TryComp<ReplicatorColossusSpawnerComponent>(args.Spawner, out var spawner) && spawner.Foundry is { } foundry)
            QueueDel(foundry);

        StartLaunch(ent);
    }

    public void StartLaunch(Entity<ReplicatorColossusComponent> ent)
    {
        var now = _timing.CurTime;
        var launch = EnsureComp<ReplicatorColossusLaunchComponent>(ent);
        launch.StartTime = now;
        launch.EndTime = now + ent.Comp.LaunchDuration;
        Dirty(ent, launch);

        _physics.SetCanCollide(ent, false);

        if (ent.Comp.LaunchLight is { } light)
            Spawn(light, Transform(ent).Coordinates);
    }

    private void OnPlayerAttached(Entity<ReplicatorColossusComponent> ent, ref PlayerAttachedEvent args)
    {
        if (ent.Comp.LaunchSoundPlayed)
            return;

        ent.Comp.LaunchSoundPlayed = true;
        _audio.PlayGlobal(ent.Comp.LaunchSound, Filter.Broadcast(), true);
    }

    private void FinishLaunch(Entity<ReplicatorColossusComponent> ent)
    {
        RemCompDeferred<ReplicatorColossusLaunchComponent>(ent);

        var xform = Transform(ent);
        ClearLaunchArea(ent, xform);
        _physics.SetCanCollide(ent, true);

        if (ent.Comp.LaunchEffect is { } effect)
            Spawn(effect, xform.Coordinates);

        var location = FormattedMessage.RemoveMarkupOrThrow(_navMap.GetNearestBeaconString((ent.Owner, xform)));
        _chat.DispatchGlobalAnnouncement(Loc.GetString(ent.Comp.Announcement, ("location", location)),
            Loc.GetString("comms-console-announcement-title-centcom"),
            playSound: false,
            colorOverride: Color.Red);

        if (_station.GetOwningStation(ent.Owner, xform) is { } station
            && TryComp<AlertLevelComponent>(station, out var alert)
            && !alert.IsLevelLocked)
        {
            _alertLevel.SetLevel((station, alert), ent.Comp.AlertLevel, playSound: false, announce: true, force: true);
        }
    }

    private void ClearLaunchArea(Entity<ReplicatorColossusComponent> ent, TransformComponent xform)
    {
        var origin = _transform.GetWorldPosition(xform);

        if (xform.GridUid is { } gridUid && TryComp<MapGridComponent>(gridUid, out var grid))
        {
            var center = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
            var reach = (int) MathF.Ceiling(ent.Comp.ClearRadius);
            for (var x = -reach; x <= reach; x++)
            {
                for (var y = -reach; y <= reach; y++)
                {
                    var indices = center + new Vector2i(x, y);
                    if (Vector2.Distance(_map.GridTileToWorldPos(gridUid, grid, indices), origin) <= ent.Comp.ClearRadius + 0.5f)
                        DemolishTile((gridUid, grid), indices);
                }
            }
        }

        _mobs.Clear();
        _lookup.GetEntitiesInRange(xform.Coordinates, ent.Comp.ClearRadius, _mobs);
        foreach (var mob in _mobs)
        {
            if (mob.Owner == ent.Owner || HasComp<ReplicatorComponent>(mob))
                continue;

            var away = _transform.GetWorldPosition(mob) - origin;
            if (away.LengthSquared() < 0.01f)
                away = _random.NextAngle().ToWorldVec();

            _throwing.TryThrow(mob, Vector2.Normalize(away) * ent.Comp.LaunchThrowDistance, 8f, ent, 0f, recoil: false);
            _stun.TryKnockdown(mob.Owner, ent.Comp.LaunchKnockdown);
        }

        _cameras.Clear();
        _lookup.GetEntitiesInRange(xform.Coordinates, LaunchKickRange, _cameras);
        foreach (var camera in _cameras)
        {
            var away = _transform.GetWorldPosition(camera) - origin;
            var strength = 1f - Math.Clamp(away.Length() / LaunchKickRange, 0f, 1f);
            var direction = away.LengthSquared() < 0.01f ? _random.NextAngle().ToWorldVec() : Vector2.Normalize(away);
            _cameraRecoil.KickCamera(camera, direction * strength * 2f, camera.Comp);
        }
    }

    private void OnMobStateChanged(Entity<ReplicatorColossusComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        _colossusDestroyed = true;

        if (TryComp<ReplicatorColossusAssimilationComponent>(ent, out var assimilation) && assimilation.BeaconEntity is { } beacon)
        {
            if (TryComp<ReplicatorColossusBeaconComponent>(beacon, out var beaconComp))
                EndLink((beacon, beaconComp), false);

            QueueDel(beacon);
        }

        var xform = Transform(ent);
        var wreck = Spawn(ent.Comp.Wreck, xform.Coordinates);
        _transform.SetLocalRotation(wreck, xform.LocalRotation);

        _explosion.QueueExplosion(_transform.GetMapCoordinates(ent, xform),
            ent.Comp.Explosion,
            ent.Comp.ExplosionIntensity,
            ent.Comp.ExplosionSlope,
            ent.Comp.ExplosionMaxTileIntensity,
            ent,
            canCreateVacuum: false);

        _chat.DispatchGlobalAnnouncement(Loc.GetString(ent.Comp.DefeatAnnouncement),
            Loc.GetString("comms-console-announcement-title-centcom"),
            playSound: false,
            colorOverride: Color.Red);

        if (_station.GetOwningStation(ent.Owner, xform) is { } station
            && _alertLevel.TryGetLevel(station, out var level)
            && level == ent.Comp.AlertLevel)
        {
            _alertLevel.SetLevel(station, ent.Comp.DefeatAlertLevel, force: true, locked: false);
        }

        QueueDel(ent);
    }

    private void DemolishTile(Entity<MapGridComponent> grid, Vector2i indices)
    {
        _anchored.Clear();
        _map.GetAnchoredEntities(grid, indices, _anchored);
        if (_anchored.Count == 0)
            return;

        var hull = BordersSpace(grid, indices);
        foreach (var uid in _anchored)
        {
            if (IsObstacle(uid) && !(hull && HasComp<AirtightComponent>(uid)))
                _destructible.DestroyEntity(uid);
        }
    }

    private bool IsObstacle(EntityUid uid)
    {
        return TryComp<PhysicsComponent>(uid, out var body)
               && body.CanCollide
               && body.Hard
               && (body.CollisionLayer & (int) ObstacleLayers) != 0
               && HasComp<DestructibleComponent>(uid)
               && !HasComp<ReplicatorNestComponent>(uid);
    }

    private bool BordersSpace(Entity<MapGridComponent> grid, Vector2i indices)
    {
        foreach (var dir in CardinalDirections)
        {
            if (!_map.TryGetTileRef(grid, grid, indices.Offset(dir), out var tile) || _turf.IsSpace(tile))
                return true;
        }

        return false;
    }
}
