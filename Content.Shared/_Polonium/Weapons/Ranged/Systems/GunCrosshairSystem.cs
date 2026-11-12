using Content.Shared._Polonium.Crosshairs;
using Content.Shared._Polonium.Weapons.Ranged.Components;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Weapons.Ranged.Systems;

/// <summary>
/// Picks the combat-mode crosshair of a gun from its <see cref="GunCrosshairComponent"/> and the round it fires next.
/// </summary>
public sealed partial class GunCrosshairSystem : EntitySystem
{
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedGunSystem _gun = default!;

    /// <summary>
    /// Picks the crosshair for the gun and the cone its next round spreads its pellets over, zero for a single projectile.
    /// A gun without its own crosshair uses the one of its <paramref name="user"/>, if they have one.
    /// Returns false when the gun has no round to fire, the crosshair is then the one the gun uses for single projectiles.
    /// </summary>
    public bool TryGetCrosshair(EntityUid gun, EntityUid? user, out ProtoId<CrosshairPrototype> crosshair, out Angle pelletSpread)
    {
        crosshair = GunCrosshairComponent.DefaultCrosshair;
        ProtoId<CrosshairPrototype>? pelletCrosshair = GunCrosshairComponent.DefaultPelletCrosshair;
        if (TryComp<GunCrosshairComponent>(gun, out var comp))
        {
            crosshair = comp.Crosshair;
            pelletCrosshair = comp.PelletCrosshair;
        }
        else if (TryComp<GunUserCrosshairComponent>(user, out var userCrosshair))
        {
            crosshair = userCrosshair.Crosshair;
        }

        pelletSpread = Angle.Zero;
        if (GetNextProjectile(gun) is not { } projectile)
            return false;

        if (!projectile.TryComp<ProjectileSpreadComponent>(out var spread, _factory) || spread.Count < 2)
            return true;

        var ev = new GunGetAmmoSpreadEvent(spread.Spread);
        RaiseLocalEvent(gun, ref ev);
        pelletSpread = ev.Spread;
        crosshair = pelletCrosshair ?? crosshair;
        return true;
    }

    private EntityPrototype? GetNextProjectile(EntityUid gun)
    {
        if (HasComp<ChamberMagazineAmmoProviderComponent>(gun))
            return _gun.GetChamberEntity(gun) is { } chambered ? FiredBy(chambered) : null;

        if (HasComp<MagazineAmmoProviderComponent>(gun))
        {
            return _container.TryGetContainer(gun, SharedGunSystem.MagazineSlot, out var slot) && slot.ContainedEntities.Count > 0
                ? GetNextProjectile(slot.ContainedEntities[0])
                : null;
        }

        if (TryComp<BallisticAmmoProviderComponent>(gun, out var ballistic))
        {
            if (ballistic.Entities.Count > 0)
                return FiredBy(ballistic.Entities[^1]);

            return ballistic.UnspawnedCount > 0 && ballistic.Proto is { } proto ? FiredBy(proto) : null;
        }

        if (TryComp<RevolverAmmoProviderComponent>(gun, out var revolver))
        {
            var index = revolver.CurrentIndex;
            if (index < revolver.AmmoSlots.Count && revolver.AmmoSlots[index] is { } round)
                return FiredBy(round);

            return index < revolver.Chambers.Length && revolver.Chambers[index] == true && revolver.FillPrototype is { } fill
                ? FiredBy(fill)
                : null;
        }

        if (TryComp<BatteryAmmoProviderComponent>(gun, out var battery))
            return FiredBy(battery.Prototype);

        if (TryComp<BasicEntityAmmoProviderComponent>(gun, out var basic))
            return FiredBy(basic.Proto);

        return null;
    }

    private EntityPrototype? FiredBy(EntityUid round)
    {
        if (TryComp<CartridgeAmmoComponent>(round, out var cartridge))
            return cartridge.Spent ? null : _proto.Index(cartridge.Prototype);

        return MetaData(round).EntityPrototype;
    }

    private EntityPrototype? FiredBy(EntProtoId round)
    {
        if (!_proto.TryIndex(round, out var proto))
            return null;

        return proto.TryComp<CartridgeAmmoComponent>(out var cartridge, _factory)
            ? _proto.Index(cartridge.Prototype)
            : proto;
    }
}
