using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools.Components;
using Content.Shared.Vehicles;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Vehicles;

public sealed partial class TankRepairSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private const float HealAmount = 50f;
    private const float Delay = 3f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TankTurretComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<TankTurretComponent, TankRepairDoAfterEvent>(OnDoAfter);
    }

    private void OnInteractUsing(EntityUid uid, TankTurretComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!HasComp<WelderComponent>(args.Used))
            return;

        if (component.AccumulatedDamage <= 0)
        {
            _popup.PopupEntity("Czolg nie wymaga naprawy.", uid, args.User);
            args.Handled = true;
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, Delay, new TankRepairDoAfterEvent(), uid, uid, args.Used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            args.Handled = true;
    }

    private void OnDoAfter(EntityUid uid, TankTurretComponent component, TankRepairDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (component.AccumulatedDamage <= 0)
            return;

        var amount = FixedPoint2.New(-HealAmount);
        var heal = new DamageSpecifier
        {
            DamageDict =
            {
                ["Blunt"] = amount,
                ["Slash"] = amount,
                ["Piercing"] = amount,
                ["Heat"] = amount,
                ["Structural"] = amount,
                ["Shock"] = amount,
            }
        };

        _damageable.TryChangeDamage(uid, heal, true);

        component.AccumulatedDamage = Math.Max(0f, component.AccumulatedDamage - HealAmount);
        Dirty(uid, component);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Items/welder.ogg"), uid);
        _popup.PopupEntity($"Naprawiono czolg. ({component.AccumulatedDamage:0}/{component.MaxDamage:0})", uid, args.User);
        args.Handled = true;
    }
}