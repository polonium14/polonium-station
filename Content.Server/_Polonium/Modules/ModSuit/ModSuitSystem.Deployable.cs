using Content.Shared._Polonium.Modules.ModSuit;
using Content.Shared._Polonium.Modules.ModSuit.Components;
using Robust.Shared.Containers;

namespace Content.Server._Polonium.Modules.ModSuit;

public partial class ModSuitSystem
{
    protected override void OnDeployableInit(Entity<ModSuitPartDeployableComponent> ent, ref ComponentInit args)
    {
        foreach (var (slot, _) in ent.Comp.DeployablePartIds)
        {
            var container = Container.EnsureContainer<ContainerSlot>(ent.Owner, GetDeployableSlotId(slot));
            ent.Comp.DeployableContainers.Add(slot, container);
        }
    }

    private void OnDeployableMapInit(Entity<ModSuitPartDeployableComponent> ent, ref MapInitEvent args)
    {
        foreach (var (slot, partId) in ent.Comp.DeployablePartIds)
        {
            var container = ent.Comp.DeployableContainers[slot];
            if (container.ContainedEntity != null)
                continue;

            var part = Spawn(partId);

            if (!Container.Insert(part, container))
            {
                Del(part);
                continue;
            }

            var deployedComp = new ModSuitDeployedPartComponent();
            deployedComp.Suit = ent.Owner;
            deployedComp.Slot = slot;
            AddComp(part, deployedComp, true);

            var typeComp = new ModSuitPartTypeComponent();
            typeComp.Type = ModSuitPartTypeHelpers.SlotToPart(slot);
            AddComp(part, typeComp, true);
        }

        var controlComp = new ModSuitPartTypeComponent();
        controlComp.Type = ModSuitPartType.Control;
        AddComp(ent.Owner, controlComp, true);

        Dirty(ent);
    }
}
