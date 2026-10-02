using System.Numerics;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Vehicles;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Vehicles;

/// <summary>
/// F = działo, R = KM.
/// Spawn: pozycja czołgu + ToWorldVec(kąt) * dystans (prosto z lufy, bez boku).
/// </summary>
public sealed partial class TankShootSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private const float BulletSpeed = 30f;

    /// <summary>Odległość spawnu od środka wzdłuż lufy (działo).</summary>
    private const float MainSpawnDist = 2.4f;

    /// <summary>Odległość spawnu od środka wzdłuż lufy (KM).</summary>
    private const float MgSpawnDist = 1.8f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<TankShootEvent>(OnShootRequest);
    }

    private void OnShootRequest(TankShootEvent msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;
        if (player == null)
            return;

        if (!TryComp(player.Value, out RelayInputMoverComponent? relay))
            return;

        var tank = relay.RelayEntity;
        if (!tank.IsValid())
            return;

        if (!TryComp(tank, out TankTurretComponent? turret))
            return;

        if (!TryComp(tank, out TransformComponent? xform))
            return;

        turret.TurretAngle = msg.AimAngle;
        Dirty(tank, turret);

        var now = _timing.CurTime;

        if (msg.MachineGun)
            TryFireMg(tank, turret, xform, player.Value, now, msg.AimAngle, msg.WorldPosition);
        else
            TryFireMain(tank, turret, xform, player.Value, now, msg.AimAngle, msg.WorldPosition);
    }

    private void TryFireMain(
        EntityUid tank,
        TankTurretComponent turret,
        TransformComponent xform,
        EntityUid player,
        TimeSpan now,
        Angle aim,
        Vector2 worldPos)
    {
        if (now < turret.NextMainFire)
        {
            TryPopupCd(tank, turret, player, now, $"Dzialo: {(turret.NextMainFire - now).TotalSeconds:0.0}s");
            return;
        }

        SpawnBullet(tank, xform, aim, worldPos, turret.MainProjectile, MainSpawnDist);
        turret.NextMainFire = now + TimeSpan.FromSeconds(turret.MainFireRate);
        Dirty(tank, turret);

        if (turret.SoundMain != null)
            _audio.PlayPvs(turret.SoundMain, tank);
    }

    private void TryFireMg(
        EntityUid tank,
        TankTurretComponent turret,
        TransformComponent xform,
        EntityUid player,
        TimeSpan now,
        Angle aim,
        Vector2 worldPos)
    {
        if (turret.MgReloading)
        {
            if (now < turret.MgReloadEnd)
            {
                TryPopupCd(tank, turret, player, now, $"KM przeladowanie: {(turret.MgReloadEnd - now).TotalSeconds:0.0}s");
                return;
            }

            turret.MgReloading = false;
            turret.MgBurstEnd = now + TimeSpan.FromSeconds(turret.MgBurstDuration);
        }

        if (now >= turret.MgBurstEnd && turret.MgBurstEnd != TimeSpan.Zero)
        {
            turret.MgReloading = true;
            turret.MgReloadEnd = now + TimeSpan.FromSeconds(turret.MgReloadTime);
            Dirty(tank, turret);
            TryPopupCd(tank, turret, player, now, "KM: przeladowanie");
            return;
        }

        if (turret.MgBurstEnd == TimeSpan.Zero)
            turret.MgBurstEnd = now + TimeSpan.FromSeconds(turret.MgBurstDuration);

        if (now < turret.NextMgFire)
            return;

        SpawnBullet(tank, xform, aim, worldPos, turret.MgProjectile, MgSpawnDist);
        turret.NextMgFire = now + TimeSpan.FromSeconds(turret.MgFireRate);
        Dirty(tank, turret);

        if (turret.SoundMg != null)
            _audio.PlayPvs(turret.SoundMg, tank);
    }

    private void TryPopupCd(EntityUid tank, TankTurretComponent turret, EntityUid player, TimeSpan now, string text)
    {
        if (now < turret.NextCdPopup)
            return;

        turret.NextCdPopup = now + TimeSpan.FromSeconds(1.0);
        Dirty(tank, turret);
        _popup.PopupEntity(text, tank, player);
    }

    /// <summary>
    /// Jak w starej działającej wersji:
    /// kierunek = AimAngle.ToWorldVec(), spawn = pozycja + kierunek * dystans.
    /// </summary>
    private void SpawnBullet(
        EntityUid tank,
        TransformComponent xform,
        Angle angle,
        Vector2 worldPos,
        EntProtoId proto,
        float spawnDistance)
    {
        if (!_proto.TryIndex(proto, out _))
            return;

        // WAŻNE: ToWorldVec, nie ToVec — inaczej kąt idzie bokiem
        var dir = angle.ToWorldVec();
        if (dir.LengthSquared() < 0.0001f)
            return;

        dir = dir.Normalized();

        // Serwerowa pozycja czołgu (bardziej pewna niż sama wiadomość klienta)
        var center = _xform.GetWorldPosition(xform);
        var spawnPos = center + dir * spawnDistance;

        if (xform.MapID == MapId.Nullspace)
            return;

        var bullet = Spawn(proto, new MapCoordinates(spawnPos, xform.MapID));
        _xform.SetWorldRotation(bullet, angle);

        if (TryComp(bullet, out ProjectileComponent? proj))
        {
            proj.Shooter = tank;
            proj.Weapon = tank;
            Dirty(bullet, proj);
        }

        if (TryComp(bullet, out PhysicsComponent? phys))
            _physics.SetLinearVelocity(bullet, dir * BulletSpeed, body: phys);
    }
}