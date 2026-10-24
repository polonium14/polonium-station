using Content.Shared._Polonium.Modules.Components.Modules;
using Content.Shared._Polonium.Modules.Events;

namespace Content.Shared._Polonium.Modules.Modules;

public sealed partial class ToggleableComponentContainerModuleSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ToggleableComponentContainerModuleComponent, ModuleEnabledEvent>(OnEnabled);
        SubscribeLocalEvent<ToggleableComponentContainerModuleComponent, ModuleDisabledEvent>(OnDisabled);
    }

    private void OnEnabled(Entity<ToggleableComponentContainerModuleComponent> ent, ref ModuleEnabledEvent args)
    {
        EntityManager.AddComponents(args.Container, ent.Comp.Components, true);
    }

    private void OnDisabled(Entity<ToggleableComponentContainerModuleComponent> ent, ref ModuleDisabledEvent args)
    {
        EntityManager.RemoveComponents(args.Container, ent.Comp.Components);
    }
}
