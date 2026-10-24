using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared._Polonium.Modules.Components;
using Content.Shared._Polonium.Modules.ModSuit.Events;
using Content.Shared._Polonium.Modules.ModSuit.UI;

namespace Content.Server._Polonium.Modules;

public partial class ModuleSystem
{
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private SharedBatterySystem _battery = default!;

    private void InitializePower()
    {
        SubscribeLocalEvent<ModuleContainerPowerComponent, ModSuitGetUiEntriesEvent>(OnModSuitGetUi);
    }

    private void OnModSuitGetUi(Entity<ModuleContainerPowerComponent> ent, ref ModSuitGetUiEntriesEvent args)
    {
        BaseModSuitPowerBuiEntry entry;

        if (_powerCell.TryGetBatteryFromSlot(ent.Owner, out var battery))
            entry = new ModSuitPowerBuiEntry(_battery.GetCharge(battery.Value.AsNullable()), battery.Value.Comp.MaxCharge);
        else
            entry = new ModSuitPowerNoCellBuiEntry();

        args.Entries.Add(entry);
    }
}
