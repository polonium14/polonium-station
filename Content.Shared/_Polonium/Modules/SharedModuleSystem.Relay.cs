using Content.Shared.Clothing;
using Content.Shared._Polonium.Modules.Components;
using Content.Shared._Polonium.Modules.Components.Modules;
using Content.Shared._Polonium.Modules.Events;
using Content.Shared._Polonium.Modules.ModSuit.Events;
using Content.Shared.PowerCell;

namespace Content.Shared._Polonium.Modules;

public partial class SharedModuleSystem
{
    private void InitializeRelay()
    {
        SubscribeLocalEvent<ModuleContainerComponent, ClothingGotEquippedEvent>(RelayToModules);
        SubscribeLocalEvent<ModuleContainerComponent, ClothingGotUnequippedEvent>(RelayToModules);

        SubscribeLocalEvent<ModuleContainerComponent, ModSuitContainerPartSealedEvent>(RelayToModules);
        SubscribeLocalEvent<ModuleContainerComponent, ModSuitContainerPartUnsealedEvent>(RelayToModules);

        SubscribeLocalEvent<ModuleContainerComponent, PowerCellSlotEmptyEvent>(RelayToModules);
    }

    public void RelayToModules<T>(Entity<ModuleContainerComponent> ent, ref T args)
    {
        var ev = new ModuleRelayedEvent<T>(args, ent.Owner);
        RaiseEventToModules((ent.Owner, ent.Comp), ref ev);
    }

    public void RelayToContainer<T>(Entity<ModuleContainedComponent> ent, ref T args)
    {
        var ev = new ModuleContainerRelayedEvent<T>(args, ent.Owner);
        RaiseEventToContainer((ent.Owner, ent.Comp), ref ev);
    }
}
