using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Explosion.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Vehicles;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Events;

namespace Content.Server.Vehicles;

public sealed partial class TankHealthSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private readonly HashSet<EntityUid> _applying = new();

    private static readonly HashSet<string> ValidDamageTypes = new()
    {
        "Blunt",
        "Slash",
        "Piercing",
        "Heat",
        "Structural",
        "Caustic",
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TankTurretComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<TankTurretComponent, AttackedEvent>(OnAttacked);
        SubscribeLocalEvent<TankTurretComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private static float SumRealDamage(DamageSpecifier? spec)
    {
        if (spec == null)
            return 0f;

        float total = 0f;
        foreach (var (type, val) in spec.DamageDict)
        {
            if (!ValidDamageTypes.Contains(type))
                continue;
            if (val <= 0)
                continue;
            total += (float)val;
        }
        return total;
    }

    private void OnStartCollide(EntityUid uid, TankTurretComponent component, ref StartCollideEvent args)
    {
        if (!TryComp(args.OtherEntity, out ProjectileComponent? proj))
            return;

        if (proj.Shooter == uid || proj.Weapon == uid)
            return;

        var amount = SumRealDamage(proj.Damage);

        if (HasComp<ExplosiveComponent>(args.OtherEntity))
        {
            var boom = 0f;
            if (TryComp(args.OtherEntity, out ExplosiveComponent? explosive))
            {
                boom = explosive.TotalIntensity;
                if (boom < 1f)
                    boom = explosive.MaxIntensity;
            }

            if (boom < 1f)
                boom = 100f;

            amount += boom;
        }
        else
        {
            amount *= 0.4f;
        }

        if (amount < 0.5f)
            return;

        ApplyTankDamage(uid, component, amount);
    }

    private void OnAttacked(EntityUid uid, TankTurretComponent component, AttackedEvent args)
    {
        var amount = SumRealDamage(args.BonusDamage) * 0.5f;
        if (amount < 0.5f)
            return;

        ApplyTankDamage(uid, component, amount);
    }

    private void OnDamageChanged(EntityUid uid, TankTurretComponent component, DamageChangedEvent args)
    {
        if (_applying.Contains(uid))
            return;

        if (!args.DamageIncreased || args.DamageDelta == null)
            return;

        var delta = SumRealDamage(args.DamageDelta);
        if (delta < 0.5f)
            return;

        ApplyTankDamage(uid, component, delta);
    }

    private void ApplyTankDamage(EntityUid uid, TankTurretComponent component, float amount)
    {
        if (amount < 0.5f)
            return;

        if (!_applying.Add(uid))
            return;

        try
        {
            component.AccumulatedDamage += amount;
            Dirty(uid, component);

            var dmg = new DamageSpecifier
            {
                DamageDict = { ["Blunt"] = amount }
            };
            _damageable.TryChangeDamage(uid, dmg, true);

            _popup.PopupEntity($"Czolg: {component.AccumulatedDamage:0}/{component.MaxDamage:0} (+{amount:0})", uid);

            if (component.AccumulatedDamage < component.MaxDamage)
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