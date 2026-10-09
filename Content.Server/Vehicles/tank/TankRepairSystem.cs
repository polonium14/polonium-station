using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.Vehicles;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Vehicles;

/// <summary>
/// Srubokret = toggle paneli (otwarte = stojacy czolg).
/// Spawarka = pancerz. Klucz = kadlub.
/// </summary>
public sealed partial class TankRepairSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _moveSpeed = default!;

    private const float ArmorHealPerUse = 50f;
    private const float HullHealPerUse = 50f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TankTurretComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<TankTurretComponent, TankRepairDoAfterEvent>(OnRepairFinished);
        SubscribeLocalEvent<TankTurretComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    // Otwarte panele = predkosc 0
    private void OnRefreshSpeed(EntityUid uid, TankTurretComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (component.PanelsOpen)
            args.ModifySpeed(0f, 0f);
    }

    private void OnInteractUsing(EntityUid uid, TankTurretComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var used = args.Used;
        var user = args.User;

        // --- Srubokret: toggle paneli ---
        if (_tool.HasQuality(used, "Screwing"))
        {
            args.Handled = true;

            _doAfter.TryStartDoAfter(new DoAfterArgs(
                EntityManager, user, 2.0f, new TankRepairDoAfterEvent(0), uid, uid, used)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = true,
            });
            return;
        }

        // --- Spawarka: pancerz ---
        if (HasComp<WelderComponent>(used) || _tool.HasQuality(used, "Welding"))
        {
            args.Handled = true;

            if (component.ArmorDestroyed)
            {
                _popup.PopupEntity("Pancerz zniszczony — nie da sie naprawic.", uid, user);
                return;
            }

            if (!component.PanelsOpen)
            {
                _popup.PopupEntity("Najpierw odkrec panele (srubokret).", uid, user);
                return;
            }

            if (component.ArmorDamage <= 0f)
            {
                _popup.PopupEntity("Pancerz jest caly.", uid, user);
                return;
            }

            _doAfter.TryStartDoAfter(new DoAfterArgs(
                EntityManager, user, 3.0f, new TankRepairDoAfterEvent(1), uid, uid, used)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = true,
            });
            return;
        }

        // --- Klucz: kadlub (Wrenching + Anchoring) ---
        if (_tool.HasQuality(used, "Wrenching") || _tool.HasQuality(used, "Anchoring"))
        {
            args.Handled = true;

            if (!component.PanelsOpen)
            {
                _popup.PopupEntity("Najpierw odkrec panele (srubokret).", uid, user);
                return;
            }

            if (component.HullDamage <= 0f)
            {
                _popup.PopupEntity("Kadlub jest caly.", uid, user);
                return;
            }

            _doAfter.TryStartDoAfter(new DoAfterArgs(
                EntityManager, user, 3.0f, new TankRepairDoAfterEvent(2), uid, uid, used)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = true,
            });
        }
    }

    private void OnRepairFinished(EntityUid uid, TankTurretComponent component, TankRepairDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        switch (args.RepairMode)
        {
            case 0: // toggle paneli
                component.PanelsOpen = !component.PanelsOpen;
                Dirty(uid, component);
                _moveSpeed.RefreshMovementSpeedModifiers(uid);

                if (component.PanelsOpen)
                    _popup.PopupEntity("Panele odkrecone. Czolg unieruchomiony.", uid, args.User);
                else
                    _popup.PopupEntity("Panele zamkniete. Czolg moze jezdzic.", uid, args.User);
                break;

            case 1: // pancerz
                if (component.ArmorDestroyed)
                {
                    _popup.PopupEntity("Pancerz zniszczony — za pozno na spawe.", uid, args.User);
                    return;
                }

                var armorHeal = MathF.Min(ArmorHealPerUse, component.ArmorDamage);
                component.ArmorDamage -= armorHeal;
                Dirty(uid, component);
                _popup.PopupEntity(
                    $"Pancerz: {component.ArmorDamage:0}/{component.MaxArmor:0} (-{armorHeal:0})",
                    uid,
                    args.User);
                break;

            case 2: // kadlub
                var hullHeal = MathF.Min(HullHealPerUse, component.HullDamage);
                component.HullDamage -= hullHeal;
                Dirty(uid, component);
                _popup.PopupEntity(
                    $"Kadlub: {component.HullDamage:0}/{component.MaxHull:0} (-{hullHeal:0})",
                    uid,
                    args.User);
                break;
        }
    }
}