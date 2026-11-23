using Content.Shared._Polonium.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Network;

namespace Content.Shared._Polonium.Weapons.Ranged.Systems;

/// <summary>
/// Removes what a shot from a gun with <see cref="TracelessGunComponent"/> would leave behind.
/// </summary>
public sealed partial class TracelessGunSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TracelessGunComponent, AmmoShotEvent>(OnAmmoShot);
        SubscribeLocalEvent<TracelessGunComponent, GunShotEvent>(OnGunShot);
    }

    private void OnAmmoShot(Entity<TracelessGunComponent> ent, ref AmmoShotEvent args)
    {
        foreach (var projectile in args.FiredProjectiles)
        {
            EnsureComp<ProjectileTracerHiddenComponent>(projectile);
        }
    }

    private void OnGunShot(Entity<TracelessGunComponent> ent, ref GunShotEvent args)
    {
        // SharedGunSystem burns the casing of an ordinary shot, this catches the rest, such as an execution
        if (_net.IsClient)
            return;

        foreach (var (ammo, shootable) in args.Ammo)
        {
            if (ammo is { } casing && shootable is CartridgeAmmoComponent { Spent: true } && !TerminatingOrDeleted(casing))
                QueueDel(casing);
        }
    }
}
