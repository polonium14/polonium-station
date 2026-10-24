using Content.Shared._Polonium.Modules.Components.Modules;
using Content.Shared._Polonium.Modules.Events;
using Content.Shared._Polonium.Modules.ModSuit;

namespace Content.Shared._Polonium.Modules.Modules;

public sealed partial class ModSuitDeployableContainerVisualsSystem : EntitySystem
{
    [Dependency] private SharedModuleSystem _module = default!;
    [Dependency] private SharedModSuitSystem _modSuit = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModSuitDeployableContainerVisualsComponent, ModuleContainerVisualsGetVisualEntityEvent>(OnGetVisualEntity);
    }

    private void OnGetVisualEntity(Entity<ModSuitDeployableContainerVisualsComponent> ent, ref ModuleContainerVisualsGetVisualEntityEvent args)
    {
        if (!_module.TryGetContainer(ent.Owner, out var container))
            return;

        if (_modSuit.TryGetDeployedPart(container.Value, ent.Comp.PartType, out var foundPart))
            args.Entity = foundPart;
    }
}
