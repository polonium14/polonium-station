using Content.Shared._Polonium.Modules.ModSuit.Components;
using Robust.Shared.Containers;

namespace Content.Client._Polonium.Modules.ModSuit;

public partial class ModSuitSystem
{
    private void InitializeDeployable()
    {
        InitializeDeployableRelay();
    }

    protected override void OnDeployableInit(Entity<ModSuitPartDeployableComponent> ent, ref ComponentInit args)
    {
        foreach (var (slot, _) in ent.Comp.DeployablePartIds)
        {
            var container = Container.EnsureContainer<ContainerSlot>(ent.Owner, GetDeployableSlotId(slot));
            ent.Comp.DeployableContainers.Add(slot, container);
        }
    }
}
