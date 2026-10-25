using Content.Shared._Polonium.Modules.Components.Modules;
using Content.Shared._Polonium.Modules.Events;
using Content.Shared._Polonium.Modules.ModSuit;

namespace Content.Shared._Polonium.Modules.Modules;

public sealed partial class ToggleableComponentModSuitPartModuleSystem : EntitySystem
{
    [Dependency] private SharedModSuitSystem _modSuit = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ToggleableComponentModSuitPartModuleComponent, ModuleEnabledEvent>(OnEnabled);
        SubscribeLocalEvent<ToggleableComponentModSuitPartModuleComponent, ModuleDisabledEvent>(OnDisabled);
    }

    private void OnEnabled(Entity<ToggleableComponentModSuitPartModuleComponent> ent, ref ModuleEnabledEvent args)
    {
        if (!_modSuit.TryGetDeployedPart(args.Container, ent.Comp.PartType, out var foundPart))
            return;

        EntityManager.AddComponents(foundPart.Value, ent.Comp.Components, true);
    }

    private void OnDisabled(Entity<ToggleableComponentModSuitPartModuleComponent> ent, ref ModuleDisabledEvent args)
    {
        if (!_modSuit.TryGetDeployedPart(args.Container, ent.Comp.PartType, out var foundPart))
            return;

        EntityManager.RemoveComponents(foundPart.Value, ent.Comp.Components);
    }
}
