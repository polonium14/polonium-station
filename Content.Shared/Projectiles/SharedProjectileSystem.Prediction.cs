// SPDX-FileCopyrightText: 2026 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Projectiles;
using Content.Shared._RMC14.Weapons.Ranged.Prediction;
using Content.Shared.Administration.Logs;
using Content.Shared.Camera;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;

namespace Content.Shared.Projectiles;

public abstract partial class SharedProjectileSystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private SharedCameraRecoilSystem _sharedCameraRecoil = default!;
    [Dependency] private SharedColorFlashEffectSystem _color = default!;
    [Dependency] private DamageableSystem _damageableSystem = default!;
    [Dependency] private SharedGunSystem _guns = default!;

    partial void InitializeProjectilePrediction()
    {
        SubscribeLocalEvent<ProjectileComponent, StartCollideEvent>(OnStartCollide);
    }

    private void OnStartCollide(EntityUid uid, ProjectileComponent component, ref StartCollideEvent args)
    {
        if (_net.IsClient && _guns.GunPrediction && HasComp<PredictedProjectileClientComponent>(uid))
            return;

        if (args.OurFixtureId != ProjectileFixture || !args.OtherFixture.Hard
            || component.ProjectileSpent || component is { Weapon: null, OnlyCollideWhenShot: true })
            return;

        ProjectileCollide((uid, component, args.OurBody), args.OtherEntity);
    }

    public void ProjectileCollide(Entity<ProjectileComponent, PhysicsComponent> projectile, EntityUid target, bool predicted = false)
    {
        var (uid, component, ourBody) = projectile;
        if (component.ProjectileSpent)
        {
            if (_net.IsServer && component.DeleteOnCollide)
                QueueDel(uid);

            return;
        }

        var attemptEv = new ProjectileReflectAttemptEvent(uid, component, false);
        RaiseLocalEvent(target, ref attemptEv);
        if (attemptEv.Cancelled)
        {
            SetShooter(uid, component, target);
            return;
        }

        var ev = new ProjectileHitEvent(component.Damage * _damageableSystem.UniversalProjectileDamageModifier, target, component.Shooter);
        RaiseLocalEvent(uid, ref ev);
        if (ev.Handled)
            return;

        var coordinates = Transform(projectile).Coordinates;

        DamageSpecifier? modifiedDamage;
        if (_net.IsServer)
        {
            _damageableSystem.TryChangeDamage(
                target,
                ev.Damage,
                out modifiedDamage,
                component.IgnoreResistances,
                origin: component.Shooter);
        }
        else
        {
            modifiedDamage = new DamageSpecifier(ev.Damage);
            var modifyEvent = new DamageModifyEvent(ev.Damage, component.Shooter);
            RaiseLocalEvent(target, modifyEvent);
            modifiedDamage = modifyEvent.Damage;
        }

        var deleted = Deleted(target);
        var otherName = ToPrettyString(target);

        var filter = Filter.Pvs(coordinates, entityMan: EntityManager);

        // The shooter predicts the hit effects client-side, so remove them from the server-side filter
        // to avoid the flash/sound playing twice for them. Other players get the server-driven effect.
        if (_guns.GunPrediction &&
            TryComp(projectile, out PredictedProjectileServerComponent? serverProjectile) &&
            serverProjectile.Shooter is { } shooter)
        {
            filter = filter.RemovePlayer(shooter);
        }

        if (modifiedDamage is not null &&
            (Exists(component.Shooter) || Exists(component.Weapon)))
        {
            if (modifiedDamage.AnyPositive() && !deleted)
                _color.RaiseEffect(Color.Red, new List<EntityUid> { target }, filter);

            if (_net.IsServer)
            {
                var shooterOrWeapon = Exists(component.Shooter)
                    ? component.Shooter!.Value
                    : component.Weapon!.Value;

                _adminLogger.Add(LogType.BulletHit,
                    HasComp<MobStateComponent>(target) ? LogImpact.Medium : LogImpact.Low,
                    $"Projectile {ToPrettyString(uid):projectile} shot by {ToPrettyString(shooterOrWeapon):source} hit {otherName:target} and dealt {modifiedDamage.GetTotal():damage} damage");
            }
        }

        if (!deleted && filter.Count > 0)
            _guns.PlayImpactSound(target, modifiedDamage, component.SoundHit, component.ForceSound, filter, uid);

        // Kick the hit entity's camera in the direction the projectile was travelling.
        if (_net.IsServer && !deleted && !ourBody.LinearVelocity.IsLengthZero())
            _sharedCameraRecoil.KickCamera(target, ourBody.LinearVelocity.Normalized());

        // Penetration is server-authoritative (it needs destructible thresholds). The client always
        // spends the projectile on the first hit; the server decides whether it keeps going.
        if (_net.IsServer &&
            modifiedDamage is not null &&
            modifiedDamage.AnyPositive() &&
            Exists(component.Shooter))
        {
            component.ProjectileSpent = !TryPenetrate((uid, component), modifiedDamage, GetProjectileDamageRequired(target));
        }
        else
        {
            component.ProjectileSpent = true;
        }

        Dirty(uid, component);

        var additionalHits = new AfterProjectileHitEvent(projectile, target);
        RaiseLocalEvent(uid, ref additionalHits);

        if ((_net.IsServer || IsClientSide(uid)) && component.ImpactEffect != null &&
            TryComp(uid, out TransformComponent? xform))
        {
            var impactEffectEv = new ImpactEffectEvent(component.ImpactEffect, GetNetCoordinates(xform.Coordinates));
            if (_net.IsServer)
                RaiseNetworkEvent(impactEffectEv, filter);
            else
                RaiseLocalEvent(impactEffectEv);
        }

        if (!predicted && component.ProjectileSpent && component.DeleteOnCollide && (_net.IsServer || IsClientSide(uid)))
        {
            QueueDel(uid);
        }
        else if (_net.IsServer && component.ProjectileSpent && component.DeleteOnCollide)
        {
            var predictedComp = EnsureComp<PredictedProjectileHitComponent>(uid);
            predictedComp.Origin = _transform.GetMoverCoordinates(coordinates);

            var targetCoords = _transform.GetMoverCoordinates(target);
            if (predictedComp.Origin.TryDistance(EntityManager, _transform, targetCoords, out var distance))
                predictedComp.Distance = distance;

            Dirty(uid, predictedComp);
        }
    }

    /// <summary>
    /// How much damage a projectile has to deal to a target to keep penetrating through it.
    /// Server-only; the client cannot evaluate destructible thresholds.
    /// </summary>
    protected virtual FixedPoint2 GetProjectileDamageRequired(EntityUid target)
    {
        return FixedPoint2.Zero;
    }

    private bool TryPenetrate(Entity<ProjectileComponent> projectile, DamageSpecifier damage, FixedPoint2 damageRequired)
    {
        // No penetration configured -> the projectile stops on this hit.
        if (projectile.Comp.PenetrationThreshold == 0)
            return false;

        // If a damage type is required, stop the projectile if it isn't dealing that type.
        if (projectile.Comp.PenetrationDamageTypeRequirement != null)
        {
            foreach (var requiredDamageType in projectile.Comp.PenetrationDamageTypeRequirement)
            {
                if (!damage.DamageDict.Keys.Contains(requiredDamageType))
                    return false;
            }
        }

        // If the target wouldn't be destroyed, it "tanks" the shot and the projectile stops.
        if (damage.GetTotal() < damageRequired)
            return false;

        if (!projectile.Comp.ProjectileSpent)
        {
            projectile.Comp.PenetrationAmount += damageRequired;
            if (projectile.Comp.PenetrationAmount >= projectile.Comp.PenetrationThreshold)
                return false;
        }

        return true;
    }
}
