using Content.Shared.Clothing;
using Content.Shared._Polonium.Modules.Components;
using Content.Shared._Polonium.Modules.Events;
using Content.Shared._Polonium.Modules.ModSuit.Events;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using JetBrains.Annotations;
using Robust.Shared.Timing;

namespace Content.Shared._Polonium.Modules;

public partial class SharedModuleSystem
{
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private IGameTiming _timing = default!;

    private void InitializePower()
    {
        SubscribeLocalEvent<ModuleContainerPowerComponent, MapInitEvent>(OnPowerInit);

        SubscribeLocalEvent<ModuleContainerPowerComponent, ClothingGotEquippedEvent>(OnPowerEquipped);
        SubscribeLocalEvent<ModuleContainerPowerComponent, ClothingGotUnequippedEvent>(OnPowerUnequipped);

        SubscribeLocalEvent<ModuleContainerPowerComponent, ModuleContainerModuleAddedEvent>((u, c, a) => UpdatePowerDraw((u, c)));
        SubscribeLocalEvent<ModuleContainerPowerComponent, ModuleContainerModuleRemovedEvent>((u, c, a) => UpdatePowerDraw((u, c)));

        SubscribeLocalEvent<ModuleContainerPowerComponent, PowerCellChangedEvent>(OnPowerCellChanged);

        SubscribeLocalEvent<ModuleContainerPowerComponent, ModSuitSealAttemptEvent>(OnPowerSealAttempt);
        SubscribeLocalEvent<ModuleContainerPowerComponent, ModSuitDeployedPartRelayedEvent<ModSuitSealAttemptEvent>>(OnPowerSealAttemptRelayed);
    }

    private void OnPowerInit(Entity<ModuleContainerPowerComponent> ent, ref MapInitEvent args)
    {
        UpdatePowerDraw((ent.Owner, ent.Comp));
    }

    private void OnPowerEquipped(Entity<ModuleContainerPowerComponent> ent, ref ClothingGotEquippedEvent args)
    {
        _powerCell.SetDrawEnabled(ent.Owner, true);
        UpdatePowerDraw((ent.Owner, ent.Comp));
    }

    private void OnPowerUnequipped(Entity<ModuleContainerPowerComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        _powerCell.SetDrawEnabled(ent.Owner, false);
    }

    private void OnPowerCellChanged(Entity<ModuleContainerPowerComponent> ent, ref PowerCellChangedEvent args)
    {
        if (ent.Comp.NextUiUpdate > _timing.CurTime)
            return;

        ent.Comp.NextUiUpdate += ent.Comp.UiUpdateInterval;
        Dirty(ent);
        UpdateUis(ent.Owner);
    }

    private void OnPowerSealAttemptRelayed(Entity<ModuleContainerPowerComponent> ent, ref ModSuitDeployedPartRelayedEvent<ModSuitSealAttemptEvent> args)
    {
        OnPowerSealAttempt(ent, ref args.Args);
    }

    private void OnPowerSealAttempt(Entity<ModuleContainerPowerComponent> ent, ref ModSuitSealAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        // only prevent sealing
        if (!args.ShouldSeal)
            return;

        if (_powerCell.HasDrawCharge(ent.Owner))
            return;

        args.Cancel();
    }

    [PublicAPI]
    public void UpdatePowerDraw(Entity<ModuleContainerPowerComponent?, ModuleContainerComponent?, PowerCellDrawComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp3, false))
            return;

        _powerCell.SetDrawRate((ent.Owner, ent.Comp3), GetModulesPowerDraw((ent.Owner, ent.Comp1, ent.Comp2)));
    }

    [PublicAPI]
    public float GetModulesPowerDraw(Entity<ModuleContainerPowerComponent?, ModuleContainerComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp1))
            return 0f;

        var ev = new GetModulePowerDrawEvent();
        RaiseEventToModules((ent.Owner, ent.Comp2), ev);

        var baseRate = GetBaseRate(ent!);
        var draw = baseRate + ev.Additional;
        return draw;
    }

    [PublicAPI]
    public float GetBaseRate(Entity<ModuleContainerPowerComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            return 0f;

        var ev = new ModuleContainerGetBasePowerDrawRate(ent.Comp.BaseRate);
        RaiseLocalEvent(ent.Owner, ref ev);

        return ev.BaseRate;
    }
}
