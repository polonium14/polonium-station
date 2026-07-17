using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared.Camera;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Random;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared._RMC14.Weapons.Ranged.Prediction;
using Content.Shared.CombatMode;
using Content.Shared.Containers;
using Content.Shared.Database;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Configuration;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly SharedCameraRecoilSystem _recoil = default!;

    public bool GunPrediction { get; private set; }

    protected bool ClientSideGunPrediction =>
        GunPrediction && (!_netManager.IsClient || _config.GetCVar(CVars.NetPredict));

    partial void InitializePrediction()
    {
        Subs.CVar(_config, RMCCVars.RMCGunPrediction, v => GunPrediction = v, true);
    }

    public void ResetShotCounter(Entity<GunComponent> gun)
    {
        if (gun.Comp.ShotCounter == 0)
            return;

        gun.Comp.ShotCounter = 0;
        DirtyField(gun.AsNullable(), nameof(GunComponent.ShotCounter));
    }

    public List<EntityUid>? AttemptShoot(
        EntityUid user,
        Entity<GunComponent> gun,
        List<int>? predictedProjectiles = null,
        ICommonSession? userSession = null)
    {
        if (gun.Comp.FireRateModified <= 0f ||
            !_actionBlockerSystem.CanAttack(user))
        {
            return null;
        }

        var toCoordinates = gun.Comp.ShootCoordinates;

        if (toCoordinates == null)
            return null;

        var curTime = Timing.CurTime;

        var prevention = new ShotAttemptedEvent
        {
            User = user,
            Used = gun
        };
        RaiseLocalEvent(gun, ref prevention);
        if (prevention.Cancelled)
            return null;

        RaiseLocalEvent(user, ref prevention);
        if (prevention.Cancelled)
            return null;

        if (gun.Comp.NextFire > curTime)
            return null;

        var fireRate = TimeSpan.FromSeconds(1f / gun.Comp.FireRateModified);

        if (gun.Comp.SelectedMode == SelectiveFire.Burst || gun.Comp.BurstActivated)
            fireRate = TimeSpan.FromSeconds(1f / gun.Comp.BurstFireRate);

        if (gun.Comp.NextFire < curTime - fireRate || gun.Comp.ShotCounter == 0 && gun.Comp.NextFire < curTime)
            gun.Comp.NextFire = curTime;

        var shots = 0;
        var lastFire = gun.Comp.NextFire;

        while (gun.Comp.NextFire <= curTime)
        {
            gun.Comp.NextFire += fireRate;
            shots++;
        }

        DirtyField(gun.AsNullable(), nameof(GunComponent.NextFire));

        if (!gun.Comp.BurstActivated)
        {
            switch (gun.Comp.SelectedMode)
            {
                case SelectiveFire.SemiAuto:
                    shots = Math.Min(shots, 1 - gun.Comp.ShotCounter);
                    break;
                case SelectiveFire.Burst:
                    shots = Math.Min(shots, gun.Comp.ShotsPerBurstModified - gun.Comp.ShotCounter);
                    break;
                case SelectiveFire.FullAuto:
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"No implemented shooting behavior for {gun.Comp.SelectedMode}!");
            }
        }
        else
        {
            shots = Math.Min(shots, gun.Comp.ShotsPerBurstModified - gun.Comp.ShotCounter);
        }

        var originEntity = HasComp<GunUseGunOriginComponent>(gun) ? gun.Owner : user;
        var fromCoordinates = Transform(originEntity).Coordinates;

        var shotOriginEv = new BeforeAttemptShootEvent(fromCoordinates, gun.Comp.ShootOriginOffset);
        RaiseLocalEvent(user, ref shotOriginEv);

        if (shotOriginEv.Handled)
            fromCoordinates = shotOriginEv.Origin;

        var attemptEv = new AttemptShootEvent(user, null, fromCoordinates, toCoordinates);
        RaiseLocalEvent(gun, ref attemptEv);

        if (attemptEv.Cancelled)
        {
            if (attemptEv.Message != null)
                PopupSystem.PopupClient(attemptEv.Message, gun, user);

            gun.Comp.BurstActivated = false;
            gun.Comp.BurstShotsCount = 0;
            gun.Comp.NextFire = attemptEv.ResetCooldown
                ? curTime
                : TimeSpan.FromSeconds(Math.Max(lastFire.TotalSeconds + SafetyNextFire, gun.Comp.NextFire.TotalSeconds));
            return null;
        }

        fromCoordinates = attemptEv.FromCoordinates;
        toCoordinates = attemptEv.ToCoordinates;
        if (toCoordinates == null)
            return null;

        var ev = new TakeAmmoEvent(shots, [], fromCoordinates, user);

        if (shots > 0)
            RaiseLocalEvent(gun, ev);

        DebugTools.Assert(ev.Ammo.Count <= shots);
        DebugTools.Assert(shots >= 0);
        UpdateAmmoCount(gun);

        gun.Comp.ShotCounter += shots;
        DirtyField(gun.AsNullable(), nameof(GunComponent.ShotCounter));

        if (ev.Ammo.Count <= 0)
        {
            var emptyGunShotEvent = new OnEmptyGunShotEvent(user);
            RaiseLocalEvent(gun, ref emptyGunShotEvent);

            gun.Comp.BurstActivated = false;
            gun.Comp.BurstShotsCount = 0;
            gun.Comp.NextFire += TimeSpan.FromSeconds(gun.Comp.BurstCooldown);

            if (shots > 0)
            {
                PopupSystem.PopupCursor(ev.Reason ?? Loc.GetString("gun-magazine-fired-empty"), user);
                gun.Comp.NextFire = TimeSpan.FromSeconds(Math.Max(lastFire.TotalSeconds + SafetyNextFire, gun.Comp.NextFire.TotalSeconds));
                Audio.PlayPredicted(gun.Comp.SoundEmpty, gun, user);
            }

            return null;
        }

        if (gun.Comp.SelectedMode == SelectiveFire.Burst)
            gun.Comp.BurstActivated = true;

        if (gun.Comp.BurstActivated)
        {
            gun.Comp.BurstShotsCount += shots;
            if (gun.Comp.BurstShotsCount >= gun.Comp.ShotsPerBurstModified)
            {
                gun.Comp.NextFire += TimeSpan.FromSeconds(gun.Comp.BurstCooldown);
                gun.Comp.BurstActivated = false;
                gun.Comp.BurstShotsCount = 0;
            }
        }

        List<EntityUid>? projectiles = null;
        var userImpulse = false;
        if (Timing.IsFirstTimePredicted)
        {
            projectiles = Shoot(
                gun,
                ev.Ammo,
                fromCoordinates,
                toCoordinates.Value,
                out userImpulse,
                user,
                throwItems: attemptEv.ThrowItems,
                predictedProjectiles,
                userSession);
        }

        var shotEv = new GunShotEvent(user, ev.Ammo, fromCoordinates, toCoordinates.Value);
        RaiseLocalEvent(gun, ref shotEv);

        if (userImpulse && TryComp<PhysicsComponent>(user, out var userPhysics))
        {
            var shooterEv = new ShooterImpulseEvent();
            RaiseLocalEvent(user, ref shooterEv);

            if (shooterEv.Push)
                CauseImpulse(fromCoordinates, toCoordinates.Value, (user, userPhysics));
        }

        foreach (var (ent, _) in ev.Ammo)
        {
            if (ent == null)
                continue;

            if (IsClientSide(ent.Value) &&
                (HasComp<GunIgnorePredictionComponent>(gun) || projectiles == null || !projectiles.Contains(ent.Value)))
            {
                Del(ent);
            }
        }

        return projectiles;
    }

    public void Shoot(
        Entity<GunComponent> gun,
        EntityUid ammo,
        EntityCoordinates fromCoordinates,
        EntityCoordinates toCoordinates,
        out bool userImpulse,
        EntityUid? user = null,
        bool throwItems = false)
    {
        var shootable = EnsureShootable(ammo);
        Shoot(
            gun,
            new List<(EntityUid? Entity, IShootable Shootable)>(1) { (ammo, shootable) },
            fromCoordinates,
            toCoordinates,
            out userImpulse,
            user,
            throwItems);
    }

    public List<EntityUid>? Shoot(
        Entity<GunComponent> gun,
        List<(EntityUid? Entity, IShootable Shootable)> ammo,
        EntityCoordinates fromCoordinates,
        EntityCoordinates toCoordinates,
        out bool userImpulse,
        EntityUid? user = null,
        bool throwItems = false,
        List<int>? predictedProjectiles = null,
        ICommonSession? userSession = null)
    {
        userImpulse = true;

        if (user != null)
        {
            var selfEvent = new SelfBeforeGunShotEvent(user.Value, gun, ammo);
            RaiseLocalEvent(user.Value, selfEvent);
            if (selfEvent.Cancelled)
            {
                userImpulse = false;
                return null;
            }
        }

        var fromMap = TransformSystem.ToMapCoordinates(fromCoordinates);
        var toMap = TransformSystem.ToMapCoordinates(toCoordinates).Position;
        var mapDirection = toMap - fromMap.Position;
        var mapAngle = mapDirection.ToAngle();
        var angle = GetRecoilAngle(Timing.CurTime, gun, gun.Comp, mapDirection.ToAngle());

        var fromEnt = Maps.TryFindGridAt(fromMap, out var gridUid, out _)
            ? TransformSystem.WithEntityId(fromCoordinates, gridUid)
            : new EntityCoordinates(Maps.GetMapOrInvalid(fromMap.MapId), fromMap.Position);

        toMap = fromMap.Position + angle.ToVec() * mapDirection.Length();
        mapDirection = toMap - fromMap.Position;
        var gunVelocity = Physics.GetMapLinearVelocity(fromEnt);

        var shotProjectiles = new List<EntityUid>(ammo.Count);

        void MarkPredicted(EntityUid projectile, int index)
        {
            if (!_netManager.IsServer || !GunPrediction || predictedProjectiles == null || userSession == null)
                return;

            // Guns flagged to ignore prediction must render their projectiles server-authoritatively.
            // Without this the server would tag the projectile as predicted, causing the client to hide
            // it (expecting a client-side prediction that never exists), so the projectile is invisible.
            if (HasComp<GunIgnorePredictionComponent>(gun))
                return;

            if (index >= predictedProjectiles.Count)
                return;

            if (!Exists(projectile))
                return;

            var predicted = predictedProjectiles[index];
            var comp = new PredictedProjectileServerComponent
            {
                Shooter = userSession,
                ClientId = predicted,
                ClientEnt = user,
            };
            AddComp(projectile, comp, true);
            Dirty(projectile, comp);
        }

        foreach (var (ent, shootable) in ammo)
        {
            if (throwItems && ent != null)
            {
                Recoil(user, mapDirection, gun.Comp.CameraRecoilScalarModified);
                ShootOrThrow(ent.Value, mapDirection, gunVelocity, gun, user);
                continue;
            }

            switch (shootable)
            {
                case CartridgeAmmoComponent cartridge:
                    if (!cartridge.Spent)
                    {
                        if (_netManager.IsServer || ClientSideGunPrediction)
                        {
                            var uid = Spawn(cartridge.Prototype, fromEnt);
                            CreateAndFireProjectiles(uid, cartridge);

                            if (_netManager.IsClient && HasComp<GunIgnorePredictionComponent>(gun))
                            {
                                predictedProjectiles?.RemoveAll(i => i == uid.Id);
                                QueueDel(uid);
                            }

                            RaiseLocalEvent(ent!.Value, new AmmoShotEvent
                            {
                                FiredProjectiles = shotProjectiles,
                            });

                            SetCartridgeSpent(ent.Value, cartridge, true);

                            if (cartridge.DeleteOnSpawn &&
                                (_netManager.IsServer || IsClientSide(ent.Value)))
                            {
                                Del(ent.Value);
                            }
                        }
                        else
                        {
                            MuzzleFlash(gun, cartridge, mapDirection.ToAngle(), user);
                            Audio.PlayPredicted(gun.Comp.SoundGunshotModified, gun, user);
                        }
                    }
                    else
                    {
                        userImpulse = false;
                        Audio.PlayPredicted(gun.Comp.SoundEmpty, gun, user);
                    }

                    Recoil(user, mapDirection, gun.Comp.CameraRecoilScalarModified);

                    if (!cartridge.DeleteOnSpawn && !Containers.IsEntityInContainer(ent!.Value))
                        EjectCartridge(ent.Value, angle);

                    if (IsClientSide(ent!.Value))
                        Del(ent.Value);
                    else
                        Dirty(ent!.Value, cartridge);

                    break;
                case AmmoComponent newAmmo:
                    if (_netManager.IsServer || ClientSideGunPrediction)
                    {
                        CreateAndFireProjectiles(ent!.Value, newAmmo);
                    }
                    else
                    {
                        MuzzleFlash(gun, newAmmo, mapDirection.ToAngle(), user);
                        Audio.PlayPredicted(gun.Comp.SoundGunshotModified, gun, user);
                    }

                    Recoil(user, mapDirection, gun.Comp.CameraRecoilScalarModified);

                    if (Exists(ent!.Value) && !HasComp<ProjectileComponent>(ent.Value))
                    {
                        if (IsClientSide(ent.Value))
                            Del(ent.Value);
                        else if (_netManager.IsClient)
                            RemoveShootable(ent.Value);
                    }

                    MarkPredicted(ent!.Value, 0);
                    break;
                case HitscanAmmoComponent:
                    if (ent == null)
                        break;

                    if (_netManager.IsServer || ClientSideGunPrediction)
                    {
                        var hitscanEv = new HitscanTraceEvent
                        {
                            FromCoordinates = fromCoordinates,
                            ShotDirection = mapDirection.Normalized(),
                            Gun = gun,
                            Shooter = user,
                            Target = gun.Comp.Target,
                        };
                        RaiseLocalEvent(ent.Value, ref hitscanEv);
                        Del(ent);
                    }

                    Audio.PlayPredicted(gun.Comp.SoundGunshotModified, gun, user);
                    Recoil(user, mapDirection, gun.Comp.CameraRecoilScalarModified);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        RaiseLocalEvent(gun, new AmmoShotEvent
        {
            FiredProjectiles = shotProjectiles,
        });

        return shotProjectiles;

        void CreateAndFireProjectiles(EntityUid ammoEnt, AmmoComponent ammoComp)
        {
            predictedProjectiles ??= new List<int>();
            MarkPredicted(ammoEnt, 0);

            if (TryComp<ProjectileSpreadComponent>(ammoEnt, out var ammoSpreadComp))
            {
                var spreadEvent = new GunGetAmmoSpreadEvent(ammoSpreadComp.Spread);
                RaiseLocalEvent(gun, ref spreadEvent);

                var angles = LinearSpread(
                    mapAngle - spreadEvent.Spread / 2,
                    mapAngle + spreadEvent.Spread / 2,
                    ammoSpreadComp.Count);

                ShootOrThrow(ammoEnt, angles[0].ToVec(), gunVelocity, gun, user);
                shotProjectiles.Add(ammoEnt);

                for (var i = 1; i < ammoSpreadComp.Count; i++)
                {
                    var newUid = Spawn(ammoSpreadComp.Proto, fromEnt);
                    ShootOrThrow(newUid, angles[i].ToVec(), gunVelocity, gun, user);
                    shotProjectiles.Add(newUid);
                    MarkPredicted(newUid, i);
                }
            }
            else
            {
                ShootOrThrow(ammoEnt, mapDirection, gunVelocity, gun, user);
                shotProjectiles.Add(ammoEnt);
            }

            MuzzleFlash(gun, ammoComp, mapDirection.ToAngle(), user);
            Audio.PlayPredicted(gun.Comp.SoundGunshotModified, gun, user);
        }
    }

    private void ShootOrThrow(
        EntityUid uid,
        Vector2 mapDirection,
        Vector2 gunVelocity,
        Entity<GunComponent> gun,
        EntityUid? user)
    {
        if (gun.Comp.Target is { } target && !TerminatingOrDeleted(target))
        {
            var targeted = EnsureComp<TargetedProjectileComponent>(uid);
            targeted.Target = target;
            Dirty(uid, targeted);
        }

        if (!HasComp<ProjectileComponent>(uid))
        {
            if (_netManager.IsClient && !ClientSideGunPrediction)
                RemoveShootable(uid);

            if (Containers.TryGetContainingContainer(uid, out var throwContainer))
                Containers.Remove(uid, throwContainer);

            ThrowingSystem.TryThrow(uid, mapDirection, gun.Comp.ProjectileSpeedModified, user);
            return;
        }

        if (Containers.TryGetContainingContainer(uid, out var container))
            Containers.Remove(uid, container);

        ShootProjectile(uid, mapDirection, gunVelocity, gun, user, gun.Comp.ProjectileSpeedModified);
    }

    private Angle GetRecoilAngle(TimeSpan curTime, EntityUid gunUid, GunComponent component, Angle direction)
    {
        var timeSinceLastFire = (curTime - component.LastFire).TotalSeconds;
        var newTheta = MathHelper.Clamp(
            component.CurrentAngle.Theta + component.AngleIncreaseModified.Theta - component.AngleDecayModified.Theta * timeSinceLastFire,
            component.MinAngleModified.Theta,
            component.MaxAngleModified.Theta);
        component.CurrentAngle = new Angle(newTheta);
        component.LastFire = component.NextFire;

        long tick = Timing.CurTick.Value;
        tick = tick << 32;
        tick = tick | (uint) GetNetEntity(gunUid).Id;
        var random = new Xoroshiro64S(tick).NextFloat(-0.5f, 0.5f);
        var angle = new Angle(direction.Theta + component.CurrentAngle.Theta * random);
        DebugTools.Assert(component.CurrentAngle.Theta * random <= component.MaxAngleModified.Theta);
        return angle;
    }

    private Angle[] LinearSpread(Angle start, Angle end, int intervals)
    {
        var angles = new Angle[intervals];
        DebugTools.Assert(intervals > 1);

        for (var i = 0; i <= intervals - 1; i++)
            angles[i] = new Angle(start + (end - start) * i / (intervals - 1));

        return angles;
    }

    private void Recoil(EntityUid? user, Vector2 recoilDirection, float recoilScalar)
    {
        if (_netManager.IsServer)
            return;

        if (!Timing.IsFirstTimePredicted || user == null || recoilDirection == Vector2.Zero || recoilScalar == 0)
            return;

        _recoil.KickCamera(user.Value, recoilDirection.Normalized() * 0.5f * recoilScalar);
    }
}
