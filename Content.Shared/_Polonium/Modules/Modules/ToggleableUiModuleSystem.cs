using Content.Shared._Polonium.Modules.Components;
using Content.Shared._Polonium.Modules.Components.Modules;
using Content.Shared._Polonium.Modules.Events;
using Content.Shared._Polonium.Modules.ModSuit;
using Content.Shared._Polonium.Modules.ModSuit.Components;
using Content.Shared._Polonium.Modules.ModSuit.Events;
using Content.Shared._Polonium.Modules.ModSuit.UI;
using Content.Shared._Polonium.Modules.ModSuit.UI.Modules;
using Robust.Shared.Timing;

namespace Content.Shared._Polonium.Modules.Modules;

public sealed partial class ToggleableUiModuleSystem : EntitySystem
{
    [Dependency] private SharedModSuitSystem _modSuit = default!;
    [Dependency] private SharedModuleSystem _module = default!;
    [Dependency] private ToggleableModuleSystem _toggleableModule = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ToggleableUiModuleComponent, ModuleEnabledEvent>(OnEnabled);
        SubscribeLocalEvent<ToggleableUiModuleComponent, ModuleDisabledEvent>(OnDisabled);

        SubscribeLocalEvent<ToggleableUiModuleComponent, ModSuitGetModuleUiEvent>(OnGetModSuitUi);

        SubscribeLocalEvent<ModuleContainerComponent, ModSuitToggleButtonMessage>(OnToggleButton);
    }

    private void OnToggleButton(Entity<ModuleContainerComponent> ent, ref ModSuitToggleButtonMessage args)
    {
        if (!TryGetEntity(args.Module, out var module) || _module.GetContainer(module.Value) != ent.Owner)
            return;

        if (!CanUpdate(module.Value))
            return;

        _toggleableModule.Toggle(module.Value, args.Toggle, args.Actor);
        _modSuit.UpdateUI(ent.Owner);
    }

    private void OnEnabled(Entity<ToggleableUiModuleComponent> ent, ref ModuleEnabledEvent args)
    {
        if (_module.TryGetContainer(ent.Owner, out var container) && CanUpdate(container.Value))
            _modSuit.UpdateUI(container.Value);
    }

    private void OnDisabled(Entity<ToggleableUiModuleComponent> ent, ref ModuleDisabledEvent args)
    {
        if (_module.TryGetContainer(ent.Owner, out var container) && CanUpdate(container.Value))
            _modSuit.UpdateUI(container.Value);
    }

    private void OnGetModSuitUi(Entity<ToggleableUiModuleComponent> ent, ref ModSuitGetModuleUiEvent args)
    {
        args.BuiEntries.Add(new ModSuitBaseToggleableModuleBuiEntry(
                _toggleableModule.IsToggled(ent.Owner),
                CompOrNull<ModSuitModuleComplexityComponent>(ent.Owner)?.Complexity
            )
        );
    }

    private bool CanUpdate(Entity<ToggleableUiModuleComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return false;

        if (ent.Comp.NextButtonPress > _timing.RealTime)
            return false;

        ent.Comp.NextButtonPress = _timing.RealTime + ent.Comp.ButtonDelay;
        Dirty(ent);
        return true;
    }
}
