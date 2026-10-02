using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Vehicles;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Events;

namespace Content.Server.Vehicles;

/// <summary>
/// Pancerz → kadłub. Jeden raz na pocisk (bez ×3 i bez „dziur” z boków).
/// </summary>
public sealed partial class TankHealthSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private readonly HashSet<EntityUid> _applying = new();
    private readonly HashSet<EntityUid> _handledProj = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TankTurretComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<TankTurretComponent, AttackedEvent>(OnAttacked);
    }

    private static float SumDamage(DamageSpecifier? spec)
    {
        if (spec == null)
            return 0f;

        float total = 0f;
        foreach (var val in spec.DamageDict.Values)
        {
            if (val > 0)
                total += (float)val;
        }
        return total;
    }

    private void OnStartCollide(EntityUid uid, TankTurretComponent component, ref StartCollideEvent args)
    {
        var other = args.OtherEntity;

        if (!TryComp(other, out ProjectileComponent? proj))
            return;

        // Własne pociski — zero
        if (proj.Shooter == uid || proj.Weapon == uid)
            return;

        // Ten sam pocisk tylko raz (StartCollide bywa kilka razy)
        if (!_handledProj.Add(other))
            return;

        var amount = SumDamage(proj.Damage);
        if (amount <= 0f)
            return;

        // Zdejmij dmg z pocisku, żeby silnik gry nie doliczył drugi raz
        proj.Damage = new DamageSpecifier();
        Dirty(other, proj);

        ApplyTankDamage(uid, component, amount);
        QueueDel(other);
    }

    private void OnAttacked(EntityUid uid, TankTurretComponent component, AttackedEvent args)
    {
        var amount = SumDamage(args.BonusDamage);

        if (TryComp(args.Used, out MeleeWeaponComponent? melee))
        {
            var weaponDmg = SumDamage(melee.Damage);
            if (weaponDmg > amount)
                amount = weaponDmg;
        }

        if (amount <= 0f)
            return;

        ApplyTankDamage(uid, component, amount);
    }

    private void ApplyTankDamage(EntityUid uid, TankTurretComponent component, float amount)
    {
        if (amount <= 0f)
            return;

        if (!_applying.Add(uid))
            return;

        try
        {
            var left = amount;

            if (!component.ArmorDestroyed && component.ArmorDamage < component.MaxArmor)
            {
                var room = component.MaxArmor - component.ArmorDamage;
                var toArmor = MathF.Min(left, room);
                component.ArmorDamage += toArmor;
                left -= toArmor;

                if (component.ArmorDamage >= component.MaxArmor)
                {
                    component.ArmorDestroyed = true;
                    component.ArmorDamage = component.MaxArmor;
                    _popup.PopupEntity("Pancerz zniszczony! Nie da sie go naprawic.", uid);
                }
            }

            if (left > 0f)
                component.HullDamage += left;

            Dirty(uid, component);

            // Czerwony flash — mały Blunt tylko pod UI, nasza logika liczy Armor/Hull
            var flash = new DamageSpecifier { DamageDict = { ["Blunt"] = 0.01 } };
            _damageable.TryChangeDamage(uid, flash, true);

            var armorTxt = component.ArmorDestroyed
                ? "Pancerz: ZNISZCZONY"
                : $"Pancerz: {component.ArmorDamage:0}/{component.MaxArmor:0}";

            _popup.PopupEntity(
                $"{armorTxt} | Kadlub: {component.HullDamage:0}/{component.MaxHull:0} (+{amount:0})",
                uid);

            if (component.HullDamage < component.MaxHull)
                return;

            EjectAll(uid);
            _audio.PlayPvs(new SoundCollectionSpecifier("MetalBreak"), uid);
            _popup.PopupEntity("Czolg zniszczony!", uid);
            QueueDel(uid);
        }
        finally
        {
            _applying.Remove(uid);
        }
    }

    private void EjectAll(EntityUid tank)
    {
        if (!TryComp(tank, out ContainerManagerComponent? manager))
            return;

        foreach (var container in manager.Containers.Values)
        {
            var ents = container.ContainedEntities.ToList();
            foreach (var ent in ents)
            {
                _container.Remove(ent, container, force: true);
                RemComp<RelayInputMoverComponent>(ent);
            }
        }
    }
}