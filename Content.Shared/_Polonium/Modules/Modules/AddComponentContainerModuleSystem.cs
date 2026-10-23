using Content.Shared._Polonium.Modules.Components.Modules;
using Content.Shared._Polonium.Modules.Events;

namespace Content.Shared._Polonium.Modules.Modules;

public sealed partial class AddComponentContainerModuleSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AddComponentContainerModuleComponent, ModuleAddedContainerEvent>(OnAdded);
        SubscribeLocalEvent<AddComponentContainerModuleComponent, ModuleRemovedContainerEvent>(OnRemoved);
    }

    private void OnAdded(Entity<AddComponentContainerModuleComponent> ent, ref ModuleAddedContainerEvent args)
    {
        EntityManager.AddComponents(args.Container, ent.Comp.Components, true);
    }

    private void OnRemoved(Entity<AddComponentContainerModuleComponent> ent, ref ModuleRemovedContainerEvent args)
    {
        EntityManager.RemoveComponents(args.Container, ent.Comp.Components);
    }
}
