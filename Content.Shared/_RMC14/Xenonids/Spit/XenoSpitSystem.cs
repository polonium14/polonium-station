using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared.Projectiles;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Spit;

public sealed partial class XenoSpitSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private XenoPlasmaSystem _plasma = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoSpitComponent, XenoSpitActionEvent>(OnSpit);
        SubscribeLocalEvent<XenoSlowingSpitComponent, XenoSlowingSpitActionEvent>(OnSlowingSpit);

        SubscribeLocalEvent<XenoSpitProjectileComponent, ProjectileHitEvent>(OnSpitProjectileHit);
        SubscribeLocalEvent<XenoSlowingSpitProjectileComponent, ProjectileHitEvent>(OnSlowingSpitProjectileHit);
    }

    private void OnSpit(Entity<XenoSpitComponent> xeno, ref XenoSpitActionEvent args)
    {
        if (args.Handled || _timing.ApplyingState)
            return;

        if (!_plasma.TryRemovePlasmaPopup(xeno.Owner, xeno.Comp.PlasmaCost))
            return;

        args.Handled = true;
        Shoot(xeno.Owner, args.Target, xeno.Comp.Projectile, xeno.Comp.Sound, xeno.Comp.ProjectileSpeed);
    }

    private void OnSlowingSpit(Entity<XenoSlowingSpitComponent> xeno, ref XenoSlowingSpitActionEvent args)
    {
        if (args.Handled || _timing.ApplyingState)
            return;

        if (!_plasma.TryRemovePlasmaPopup(xeno.Owner, xeno.Comp.PlasmaCost))
            return;

        args.Handled = true;
        Shoot(xeno.Owner, args.Target, xeno.Comp.Projectile, xeno.Comp.Sound, xeno.Comp.ProjectileSpeed);
    }

    private void Shoot(EntityUid xeno, EntityCoordinates target, EntProtoId projectile, Robust.Shared.Audio.SoundSpecifier sound, float speed)
    {
        _audio.PlayPredicted(sound, xeno, xeno);

        if (_net.IsClient)
            return;

        var fromCoords = Transform(xeno).Coordinates;
        var fromMap = _transform.ToMapCoordinates(fromCoords);
        var toMap = _transform.ToMapCoordinates(target);
        var direction = toMap.Position - fromMap.Position;
        if (direction == default)
            return;

        var ent = Spawn(projectile, fromMap);
        var userVelocity = _physics.GetMapLinearVelocity(xeno);
        _gun.ShootProjectile(ent, direction, userVelocity, xeno, xeno, speed);
    }

    private void OnSpitProjectileHit(Entity<XenoSpitProjectileComponent> projectile, ref ProjectileHitEvent args)
    {
        if (!projectile.Comp.DeleteOnFriendlyXeno)
            return;

        if (!IsFriendlyXeno(args.Target))
            return;

        PredictedQueueDel(projectile);
    }

    private void OnSlowingSpitProjectileHit(Entity<XenoSlowingSpitProjectileComponent> projectile, ref ProjectileHitEvent args)
    {
        var target = args.Target;
        if (IsFriendlyXeno(target))
        {
            PredictedQueueDel(projectile);
            return;
        }

        if (_net.IsClient)
            return;

        if (projectile.Comp.Paralyze > TimeSpan.Zero)
            _stun.TryAddParalyzeDuration(target, projectile.Comp.Paralyze, visualized: true);
    }

    private bool IsFriendlyXeno(EntityUid target)
    {
        return HasComp<XenoComponent>(target) || HasComp<XenoFriendlyComponent>(target);
    }
}
