using Content.Shared._Polonium.Modules.Components.Modules;
using Content.Shared._Polonium.Modules.Events;
using Content.Shared._Polonium.Modules.Modules;
using Content.Shared._Polonium.Modules.ModSuit.Components;
using Content.Shared.Clothing;
using Content.Shared.Inventory;
using Content.Shared.Movement.Systems;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.PowerCell;
using Robust.Shared.Containers;

namespace Content.Shared._Polonium.Modules.ModSuit;

public sealed partial class ModSuitSpeedSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedModSuitSystem _modSuit = default!;
    [Dependency] private SharedModuleSystem _module = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private ToggleableModuleSystem _toggleableModule = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModSuitSpeedComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ModSuitSpeedComponent, BatteryStateChangedEvent>(OnBatteryStateChanged);
        SubscribeLocalEvent<ModSuitSpeedComponent, PowerCellChangedEvent>(OnPowerCellChanged);
        SubscribeLocalEvent<ModSuitSpeedComponent, InventoryRelayedEvent<RefreshMovementSpeedModifiersEvent>>(OnRefreshSpeed);

        SubscribeLocalEvent<SpeedModuleComponent, ModuleEnabledEvent>((_, _, a) => RefreshWearerSpeed(a.Container), after: [typeof(ToggleableModuleSystem)]);
        SubscribeLocalEvent<SpeedModuleComponent, ModuleDisabledEvent>((_, _, a) => RefreshWearerSpeed(a.Container), after: [typeof(ToggleableModuleSystem)]);
    }

    private void OnMapInit(Entity<ModSuitSpeedComponent> ent, ref MapInitEvent args)
    {
        SetUnpowered(ent, !_modSuit.HasPower(ent.Owner));
    }

    private void OnBatteryStateChanged(Entity<ModSuitSpeedComponent> ent, ref BatteryStateChangedEvent args)
    {
        SetUnpowered(ent, args.NewState == BatteryState.Empty);
    }

    private void OnPowerCellChanged(Entity<ModSuitSpeedComponent> ent, ref PowerCellChangedEvent args)
    {
        SetUnpowered(ent, args.Ejected || !_modSuit.HasPower(ent.Owner));
    }

    private void OnRefreshSpeed(Entity<ModSuitSpeedComponent> ent, ref InventoryRelayedEvent<RefreshMovementSpeedModifiersEvent> args)
    {
        if (ent.Comp.Unpowered)
        {
            // The unpowered values replace the suit's own modifier instead of stacking with it.
            var clothing = CompOrNull<ClothingSpeedModifierComponent>(ent.Owner);
            var walk = clothing?.WalkModifier ?? 1f;
            var sprint = clothing?.SprintModifier ?? 1f;
            args.Args.ModifySpeed(ent.Comp.UnpoweredWalkModifier / walk, ent.Comp.UnpoweredSprintModifier / sprint);
            return;
        }

        foreach (var module in _module.GetModules(ent.Owner))
        {
            if (TryComp<SpeedModuleComponent>(module, out var speed) && _toggleableModule.IsToggled(module))
                args.Args.ModifySpeed(speed.WalkModifier, speed.SprintModifier);
        }
    }

    private void SetUnpowered(Entity<ModSuitSpeedComponent> ent, bool unpowered)
    {
        if (ent.Comp.Unpowered == unpowered)
            return;

        ent.Comp.Unpowered = unpowered;
        Dirty(ent);
        RefreshWearerSpeed(ent.Owner);
    }

    private void RefreshWearerSpeed(EntityUid suit)
    {
        if (_container.TryGetContainingContainer((suit, null, null), out var container))
            _movementSpeed.RefreshMovementSpeedModifiers(container.Owner);
    }
}
