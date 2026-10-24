using Content.Shared._Polonium.Modules.Components.Modules;
using Content.Shared._Polonium.Modules.Events;
using Content.Shared.PowerCell;

namespace Content.Shared._Polonium.Modules.Modules;

public abstract partial class SharedPowerDrainModuleSystem : EntitySystem
{
    [Dependency] private SharedModuleSystem _module = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private ToggleableModuleSystem _toggleableModule = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PowerDrainModuleComponent, ModuleEnabledEvent>(OnEnabled);
        SubscribeLocalEvent<PowerDrainModuleComponent, ModuleDisabledEvent>(OnDisabled);

        SubscribeLocalEvent<PowerDrainModuleComponent, ModuleToggleAttemptEvent>(OnToggleAttempt);

        SubscribeLocalEvent<PowerDrainModuleComponent, GetModulePowerDrawEvent>(OnGetPower);
        SubscribeLocalEvent<PowerDrainModuleComponent, ModuleRelayedEvent<PowerCellSlotEmptyEvent>>(OnEmpty);
    }

    protected virtual void OnEnabled(Entity<PowerDrainModuleComponent> ent, ref ModuleEnabledEvent args)
    {
        _module.UpdatePowerDraw(args.Container);
    }

    private void OnDisabled(Entity<PowerDrainModuleComponent> ent, ref ModuleDisabledEvent args)
    {
        _module.UpdatePowerDraw(args.Container);
    }

    private void OnToggleAttempt(Entity<PowerDrainModuleComponent> ent, ref ModuleToggleAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        // only prevent enabling
        if (!args.Toggle)
            return;

        if (_powerCell.HasActivatableCharge(args.Container))
            return;

        args.Cancel();
    }

    private void OnGetPower(Entity<PowerDrainModuleComponent> ent, ref GetModulePowerDrawEvent args)
    {
        float? entry;

        if (_toggleableModule.IsToggled(ent.Owner))
            entry = ent.Comp.EnabledDraw;
        else
            entry = ent.Comp.DisabledDraw;

        if (entry == null)
            return;

        args.Additional += entry.Value;
    }

    private void OnEmpty(Entity<PowerDrainModuleComponent> ent, ref ModuleRelayedEvent<PowerCellSlotEmptyEvent> args)
    {
        _toggleableModule.RaiseToggleEvents(ent.Owner, false, null);
    }
}
