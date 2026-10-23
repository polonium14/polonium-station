using Content.Shared._Polonium.Modules.Components;
using Content.Shared._Polonium.Modules.Components.Modules;
using Content.Shared._Polonium.Modules.Events;
using Content.Shared._Polonium.Modules.ModSuit.Components;
using Content.Shared._Polonium.Modules.ModSuit.Events;
using Content.Shared._Polonium.Modules.ModSuit.UI;
using Content.Shared._Polonium.Modules.ModSuit.UI.Modules;
using JetBrains.Annotations;
using Robust.Shared.Timing;

namespace Content.Shared._Polonium.Modules.Modules;

public abstract partial class SharedFlashlightModuleSystem : EntitySystem
{
    [Dependency] protected SharedAppearanceSystem Appearance = default!;
    [Dependency] private SharedPointLightSystem _pointLight = default!;
    [Dependency] private SharedModuleSystem _module = default!;
    [Dependency] protected SharedModuleContainerVisualsSystem ModuleContainerVisuals = default!;
    [Dependency] private ToggleableModuleSystem _toggleableModule = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FlashlightModuleComponent, ModuleEnabledEvent>(OnEnabled);
        SubscribeLocalEvent<FlashlightModuleComponent, ModuleDisabledEvent>(OnDisabled);

        SubscribeLocalEvent<FlashlightModuleComponent, ModSuitGetModuleUiEvent>(OnGetModSuitUi);

        SubscribeLocalEvent<ModuleContainerComponent, ModSuitFlashlightColorChangedMessage>(OnFlashlightColorChanged);
    }

    private void OnEnabled(Entity<FlashlightModuleComponent> ent, ref ModuleEnabledEvent args)
    {
        if (!_module.TryGetContainer(ent.Owner, out var container))
            return;

        _pointLight.SetEnabled(container.Value, true);
        SetColor(ent.AsNullable(), ent.Comp.Color);
    }

    private void OnDisabled(Entity<FlashlightModuleComponent> ent, ref ModuleDisabledEvent args)
    {
        if (!_module.TryGetContainer(ent.Owner, out var container))
            return;

        _pointLight.SetEnabled(container.Value, false);
        SetColor(ent.AsNullable(), ent.Comp.Color);
    }

    private void OnGetModSuitUi(Entity<FlashlightModuleComponent> ent, ref ModSuitGetModuleUiEvent args)
    {
        args.BuiEntries.Add(new ModSuitFlashlightModuleBuiEntry(
                _toggleableModule.IsToggled(ent.Owner),
                ent.Comp.Color,
                CompOrNull<ModSuitModuleComplexityComponent>(ent.Owner)?.Complexity
            )
        );
    }

    private void OnFlashlightColorChanged(Entity<ModuleContainerComponent> ent, ref ModSuitFlashlightColorChangedMessage args)
    {
        if (!TryGetEntity(args.Module, out var module) || _module.GetContainer(module.Value) != ent.Owner)
            return;

        if (!TryComp<FlashlightModuleComponent>(module, out var flashlightModule))
            return;

        if (flashlightModule.NextUpdate > _timing.RealTime)
            return;

        flashlightModule.NextUpdate = _timing.RealTime + flashlightModule.UpdateRate; // no need to dirty as setting color does it

        SetColor(module.Value, args.Color);
    }

    [PublicAPI]
    public void SetColor(Entity<FlashlightModuleComponent?> ent, Color color)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return;

        ent.Comp.Color = color;
        Dirty(ent);

        if (_module.TryGetContainer(ent.Owner, out var container))
            _pointLight.SetColor(container.Value, color);

        if (ModuleContainerVisuals.GetVisualEntity(ent.Owner) is { } visual)
            Appearance.SetData(visual, FlashlightModuleVisualState.Color, color);
    }
}
