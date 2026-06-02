// SPDX-FileCopyrightText: 2022 Kara <lunarautomaton6@gmail.com>
// SPDX-FileCopyrightText: 2023 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 slarticodefast <161409025+slarticodefast@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Tadeo <td12233a@gmail.com>
// SPDX-FileCopyrightText: 2025 Toaster <mrtoastymyroasty@gmail.com>
// SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
// SPDX-FileCopyrightText: 2026 github-actions[bot] <41898282+github-actions[bot]@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Examine;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    protected virtual void InitializeBattery()
    {
        SubscribeLocalEvent<HitscanBatteryAmmoProviderComponent, ComponentGetState>(OnBatteryGetState);
        SubscribeLocalEvent<HitscanBatteryAmmoProviderComponent, ComponentHandleState>(OnBatteryHandleState);
        SubscribeLocalEvent<HitscanBatteryAmmoProviderComponent, TakeAmmoEvent>(OnBatteryTakeAmmo);
        SubscribeLocalEvent<HitscanBatteryAmmoProviderComponent, CheckShootPrototypeEvent>(OnBatteryCheckProto);
        SubscribeLocalEvent<HitscanBatteryAmmoProviderComponent, GetAmmoCountEvent>(OnBatteryAmmoCount);
        SubscribeLocalEvent<HitscanBatteryAmmoProviderComponent, ExaminedEvent>(OnBatteryExamine);

        SubscribeLocalEvent<ProjectileBatteryAmmoProviderComponent, ComponentGetState>(OnBatteryGetState);
        SubscribeLocalEvent<ProjectileBatteryAmmoProviderComponent, ComponentHandleState>(OnBatteryHandleState);
        SubscribeLocalEvent<ProjectileBatteryAmmoProviderComponent, TakeAmmoEvent>(OnBatteryTakeAmmo);
        SubscribeLocalEvent<ProjectileBatteryAmmoProviderComponent, CheckShootPrototypeEvent>(OnBatteryCheckProto);
        SubscribeLocalEvent<ProjectileBatteryAmmoProviderComponent, GetAmmoCountEvent>(OnBatteryAmmoCount);
        SubscribeLocalEvent<ProjectileBatteryAmmoProviderComponent, ExaminedEvent>(OnBatteryExamine);
    }

    private void OnBatteryHandleState(EntityUid uid, BatteryAmmoProviderComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not BatteryAmmoProviderComponentState state)
            return;

        component.Shots = state.Shots;
        component.Capacity = state.MaxShots;
        component.FireCost = state.FireCost;

        if (component is HitscanBatteryAmmoProviderComponent hitscan && state.Prototype != null)
            hitscan.HitscanEntityProto = state.Prototype;
        else if (component is ProjectileBatteryAmmoProviderComponent projectile && state.Prototype != null)
            projectile.Prototype = state.Prototype;
    }

    private void OnBatteryGetState(EntityUid uid, BatteryAmmoProviderComponent component, ref ComponentGetState args)
    {
        var state = new BatteryAmmoProviderComponentState()
        {
            Shots = component.Shots,
            MaxShots = component.Capacity,
            FireCost = component.FireCost,
        };

        if (component is HitscanBatteryAmmoProviderComponent hitscan)
            state.Prototype = hitscan.HitscanEntityProto;
        else if (component is ProjectileBatteryAmmoProviderComponent projectile)
            state.Prototype = projectile.Prototype;

        args.State = state;
    }

    private void OnBatteryExamine(EntityUid uid, BatteryAmmoProviderComponent component, ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("gun-battery-examine", ("color", AmmoExamineColor), ("count", component.Shots)));
    }

    private void OnBatteryTakeAmmo(EntityUid uid, BatteryAmmoProviderComponent component, TakeAmmoEvent args)
    {
        var shots = Math.Min(args.Shots, component.Shots);

        if (shots == 0)
            return;

        for (var i = 0; i < shots; i++)
        {
            args.Ammo.Add(GetShootable(component, args.Coordinates));
            component.Shots--;
        }

        TakeCharge(uid, component);
        UpdateBatteryAppearance(uid, component);
        Dirty(uid, component);
    }

    private void OnBatteryCheckProto(EntityUid uid, BatteryAmmoProviderComponent comp, ref CheckShootPrototypeEvent args)
    {
        switch (comp)
        {
            case ProjectileBatteryAmmoProviderComponent proj:
                ProtoManager.TryIndex(proj.Prototype, out var proto);
                args.ShootPrototype = proto;
                break;
            case HitscanBatteryAmmoProviderComponent hitscan:
                if (ProtoManager.TryIndex(hitscan.HitscanEntityProto, out EntityPrototype? hitEntProto))
                    args.ShootPrototype = hitEntProto;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void OnBatteryAmmoCount(EntityUid uid, BatteryAmmoProviderComponent component, ref GetAmmoCountEvent args)
    {
        args.Count = component.Shots;
        args.Capacity = component.Capacity;
    }

    protected virtual void TakeCharge(EntityUid uid, BatteryAmmoProviderComponent component)
    {
        UpdateAmmoCount(uid, prediction: false);
    }

    protected void UpdateBatteryAppearance(EntityUid uid, BatteryAmmoProviderComponent component)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance))
            return;

        Appearance.SetData(uid, AmmoVisuals.HasAmmo, component.Shots != 0, appearance);
        Appearance.SetData(uid, AmmoVisuals.AmmoCount, component.Shots, appearance);
        Appearance.SetData(uid, AmmoVisuals.AmmoMax, component.Capacity, appearance);
    }

    private (EntityUid? Entity, IShootable) GetShootable(BatteryAmmoProviderComponent component, EntityCoordinates coordinates)
    {
        switch (component)
        {
            case ProjectileBatteryAmmoProviderComponent proj:
                var ent = Spawn(proj.Prototype, coordinates);
                return (ent, EnsureShootable(ent));
            case HitscanBatteryAmmoProviderComponent hitscan:
                if (ProtoManager.TryIndex(hitscan.HitscanEntityProto, out EntityPrototype? entProto))
                {
                    var hitscanEnt = Spawn(hitscan.HitscanEntityProto);
                    return (hitscanEnt, EnsureShootable(hitscanEnt));
                }

                if (ProtoManager.TryIndex(hitscan.HitscanEntityProto, out HitscanPrototype? hitscanProto))
                    return (null, hitscanProto);

                throw new InvalidOperationException($"Unknown hitscan prototype: {hitscan.HitscanEntityProto}");
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    [Serializable, NetSerializable]
    private sealed class BatteryAmmoProviderComponentState : ComponentState
    {
        public int Shots;
        public int MaxShots;
        public float FireCost;
        public string? Prototype;
    }
}
