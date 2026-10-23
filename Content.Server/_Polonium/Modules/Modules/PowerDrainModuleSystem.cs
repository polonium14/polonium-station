using Content.Shared.PowerCell;
using Content.Shared._Polonium.Modules.Components.Modules;
using Content.Shared._Polonium.Modules.Events;
using Content.Shared._Polonium.Modules.Modules;

namespace Content.Server._Polonium.Modules.Modules;

public sealed partial class PowerDrainModuleSystem : SharedPowerDrainModuleSystem
{
    [Dependency] private PowerCellSystem _powerCell = default!;

    protected override void OnEnabled(Entity<PowerDrainModuleComponent> ent, ref ModuleEnabledEvent args)
    {
        base.OnEnabled(ent, ref args);

        if (ent.Comp.OnUseDraw == null)
            return;

        _powerCell.TryUseCharge(args.Container, ent.Comp.OnUseDraw.Value);
    }
}
