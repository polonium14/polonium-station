using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Content.Shared.Vehicles;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Server.Vehicles;

public sealed partial class TankShootSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;

    private readonly Dictionary<EntityUid, (Vector2 Pos, TimeSpan Time)> _lastPos = new();
    private readonly Dictionary<EntityUid, TimeSpan> _nextPopup = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<TankShootEvent>(OnShoot);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<TankTurretComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var turret, out var xform))
        {
            _lastPos[uid] = (_xform.GetWorldPosition(xform), now);

            var dirty = false;
            if (turret.MainReloading && now >= turret.NextMainFire)
            {
                turret.MainReloading = false;
                dirty = true;
            }
            if (turret.MgReloading && now >= turret.MgReloadEnd)
            {
                turret.MgReloading = false;
                dirty = true;
            }
            if (dirty)
                Dirty(uid, turret);
        }

        var toRemove = new List<EntityUid>();
        foreach (var uid in _lastPos.Keys)
        {
            if (!Exists(uid))
                toRemove.Add(uid);
        }
        foreach (var uid in toRemove)
            _lastPos.Remove(uid);

        toRemove.Clear();
        foreach (var uid in _nextPopup.Keys)
        {
            if (!Exists(uid))
                toRemove.Add(uid);
        }
        foreach (var uid in toRemove)
            _nextPopup.Remove(uid);
    }

    private void TryPopup(EntityUid tank, EntityUid player, string msg, TimeSpan now)
    {
        if (_nextPopup.TryGetValue(player, out var next) && now < next)
            return;

        _popup.PopupEntity(msg, tank, player);
        _nextPopup[player] = now + TimeSpan.FromSeconds(1);
    }

    private void OnShoot(TankShootEvent args, EntitySessionEventArgs session)
    {
        if (session.SenderSession.AttachedEntity is not { } player)
            return;

        if (TryComp(player, out MobStateComponent? playerMob) &&
            playerMob.CurrentState != MobState.Alive)
            return;

        if (!TryComp(player, out RelayInputMoverComponent? relay))
            return;

        var tank = relay.RelayEntity;
        if (!tank.IsValid())
            return;

        if (!TryComp(tank, out TankTurretComponent? turret))
            return;

        if (!TryComp(tank, out TransformComponent? xform))
            return;

        var now = _timing.CurTime;
        var aimAngle = args.AimAngle;
        turret.TurretAngle = aimAngle;

        if (args.IsMain)
        {
            if (now < turret.NextMainFire)
            {
                var left = (turret.NextMainFire - now).TotalSeconds;
                TryPopup(tank, player, $"Dzialo: {left:0.0}s", now);
                return;
            }

            FireProjectile(tank, xform, turret.MainProjectile, aimAngle, args.WorldPosition, 2.2f);
            turret.NextMainFire = now + TimeSpan.FromSeconds(turret.MainFireRate);
            turret.MainReloading = true;
            Dirty(tank, turret);
            return;
        }

        if (turret.MgReloading && now < turret.MgReloadEnd)
        {
            var left = (turret.MgReloadEnd - now).TotalSeconds;
            TryPopup(tank, player, $"KM: przeladowanie {left:0.0}s", now);
            return;
        }

        if (turret.MgReloading && now >= turret.MgReloadEnd)
        {
            turret.MgReloading = false;
            turret.MgBurstEnd = TimeSpan.Zero;
        }

        if (turret.MgBurstEnd == TimeSpan.Zero)
            turret.MgBurstEnd = now + TimeSpan.FromSeconds(turret.MgBurstDuration);

        if (now >= turret.MgBurstEnd)
        {
            turret.MgReloading = true;
            turret.MgReloadEnd = now + TimeSpan.FromSeconds(turret.MgReloadTime);
            turret.MgBurstEnd = TimeSpan.Zero;
            Dirty(tank, turret);
            TryPopup(tank, player, $"KM: przeladowanie {turret.MgReloadTime:0}s", now);
            return;
        }

        if (now < turret.NextMgFire)
            return;

        FireProjectile(tank, xform, turret.MgProjectile, aimAngle, args.WorldPosition, 1.3f);
        turret.NextMgFire = now + TimeSpan.FromSeconds(turret.MgFireRate);
        Dirty(tank, turret);
    }

    private Vector2 GetTankVelocity(EntityUid tankUid, Vector2 currentPos)
    {
        var physVel = _physics.GetMapLinearVelocity(tankUid);
        if (physVel.LengthSquared() > 0.01f)
            return physVel;

        var now = _timing.CurTime;
        if (_lastPos.TryGetValue(tankUid, out var last))
        {
            var dt = (float)(now - last.Time).TotalSeconds;
            if (dt > 0.001f && dt < 0.5f)
                return (currentPos - last.Pos) / dt;
        }

        return Vector2.Zero;
    }

    private void FireProjectile(
        EntityUid tankUid,
        TransformComponent xform,
        EntProtoId protoId,
        Angle angle,
        Vector2 clientPos,
        float spawnDistance)
    {
        if (!_proto.TryIndex(protoId, out _))
            return;

        var dir = angle.ToWorldVec();
        if (dir.LengthSquared() < 0.0001f)
            return;

        dir = dir.Normalized();

        var tankVel = GetTankVelocity(tankUid, clientPos);
        var spawnPos = clientPos + dir * spawnDistance + tankVel * 0.05f;
        var mapId = xform.MapID;

        if (mapId == MapId.Nullspace)
            return;

        var projectile = Spawn(protoId, new MapCoordinates(spawnPos, mapId));
        _xform.SetWorldRotation(projectile, angle);
        _gun.ShootProjectile(projectile, dir, tankVel, tankUid, tankUid, 55f);
    }
}