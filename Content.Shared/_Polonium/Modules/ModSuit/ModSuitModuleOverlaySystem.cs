using Content.Shared._Polonium.Modules.Components.Modules;
using Content.Shared._Polonium.Modules.Events;
using Content.Shared._Polonium.Modules.ModSuit.Components;
using Content.Shared._Polonium.Modules.Modules;

namespace Content.Shared._Polonium.Modules.ModSuit;

/// <summary>
/// Collects how the installed modules change the look of the suit into the appearance of the suit and its parts,
/// where the client draws it.
/// </summary>
public sealed partial class ModSuitModuleOverlaySystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedModSuitSystem _modSuit = default!;
    [Dependency] private SharedModuleSystem _module = default!;
    [Dependency] private ToggleableModuleSystem _toggleableModule = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModSuitModuleOverlayComponent, ModuleAddedContainerEvent>((_, _, args) => UpdateOverlays(args.Container));
        SubscribeLocalEvent<ModSuitModuleOverlayComponent, ModuleRemovedContainerEvent>((_, _, args) => UpdateOverlays(args.Container));
        SubscribeLocalEvent<ModSuitModuleOverlayComponent, ModuleEnabledEvent>((_, _, args) => UpdateOverlays(args.Container), after: [typeof(ToggleableModuleSystem)]);
        SubscribeLocalEvent<ModSuitModuleOverlayComponent, ModuleDisabledEvent>((_, _, args) => UpdateOverlays(args.Container), after: [typeof(ToggleableModuleSystem)]);

        SubscribeLocalEvent<ModSuitHidePartModuleComponent, ModuleAddedContainerEvent>((_, _, args) => UpdateOverlays(args.Container));
        SubscribeLocalEvent<ModSuitHidePartModuleComponent, ModuleRemovedContainerEvent>((_, _, args) => UpdateOverlays(args.Container));
    }

    private void UpdateOverlays(EntityUid suit)
    {
        List<string> suffixes = [];
        HashSet<ModSuitPartType> hidden = [];

        foreach (var module in _module.GetModules(suit))
        {
            if (TryComp<ModSuitModuleOverlayComponent>(module, out var overlay))
                suffixes.Add(overlay.EnabledSuffix != null && _toggleableModule.IsToggled(module) ? overlay.EnabledSuffix : overlay.Suffix);

            if (TryComp<ModSuitHidePartModuleComponent>(module, out var hide))
                hidden.Add(hide.Part);
        }

        var value = string.Join(',', suffixes);
        _appearance.SetData(suit, ModSuitSealedVisuals.Overlays, value);

        foreach (var part in _modSuit.GetAllParts(suit))
        {
            _appearance.SetData(part, ModSuitSealedVisuals.Overlays, value);
            _appearance.SetData(part, ModSuitSealedVisuals.Hidden, CompOrNull<ModSuitPartTypeComponent>(part) is { } type && hidden.Contains(type.Type));
        }
    }
}
