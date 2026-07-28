using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Spit;

public sealed partial class XenoSpitSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private XenoPlasmaSystem _plasma = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoSpitComponent, XenoSpitActionEvent>(OnSpit);
    }

    private void OnSpit(Entity<XenoSpitComponent> xeno, ref XenoSpitActionEvent args)
    {
        if (args.Handled || _timing.ApplyingState)
            return;

        if (!_plasma.TryRemovePlasmaPopup(xeno.Owner, xeno.Comp.PlasmaCost))
            return;

        args.Handled = true;
        _audio.PlayPredicted(xeno.Comp.Sound, xeno, xeno);

        if (_net.IsClient)
            return;

        var fromCoords = Transform(xeno).Coordinates;
        var fromMap = _transform.ToMapCoordinates(fromCoords);
        var toMap = _transform.ToMapCoordinates(args.Target);
        var direction = toMap.Position - fromMap.Position;
        if (direction == default)
            return;

        var ent = Spawn(xeno.Comp.Projectile, fromMap);
        var userVelocity = _physics.GetMapLinearVelocity(xeno);
        _gun.ShootProjectile(ent, direction, userVelocity, xeno, xeno, xeno.Comp.ProjectileSpeed);
    }
}
