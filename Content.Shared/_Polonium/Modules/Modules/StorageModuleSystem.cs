using System.Linq;
using Content.Shared._Polonium.Modules.Components.Modules;
using Content.Shared._Polonium.Modules.Events;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Shared._Polonium.Modules.Modules;

public sealed partial class StorageModuleSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedStorageSystem _storage = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StorageModuleComponent, ModuleAddedContainerEvent>(OnAdded);
        SubscribeLocalEvent<StorageModuleComponent, ModuleRemovedContainerEvent>(OnRemoved);
    }

    // Items are moved between the module and the suit only on the server, the client gets the result in the game state.
    private void OnAdded(Entity<StorageModuleComponent> ent, ref ModuleAddedContainerEvent args)
    {
        if (_net.IsClient || !TryComp<StorageComponent>(ent.Owner, out var moduleStorage))
            return;

        if (!TryComp<StorageComponent>(args.Container, out var containerStorage))
        {
            _storage.CopyComponent((ent.Owner, moduleStorage), args.Container);
            containerStorage = Comp<StorageComponent>(args.Container);
        }

        MoveItems((ent.Owner, moduleStorage), (args.Container, containerStorage));
    }

    private void OnRemoved(Entity<StorageModuleComponent> ent, ref ModuleRemovedContainerEvent args)
    {
        if (_net.IsClient || TerminatingOrDeleted(ent.Owner) || TerminatingOrDeleted(args.Container))
            return;

        if (!TryComp<StorageComponent>(ent.Owner, out var moduleStorage) ||
            !TryComp<StorageComponent>(args.Container, out var containerStorage))
            return;

        MoveItems((args.Container, containerStorage), (ent.Owner, moduleStorage));
        RemCompDeferred(args.Container, containerStorage);
    }

    private void MoveItems(Entity<StorageComponent> from, Entity<StorageComponent> to)
    {
        foreach (var (item, location) in from.Comp.StoredItems.ToArray())
        {
            if (_storage.InsertAt((to.Owner, to.Comp), item, location, out _, playSound: false, stackAutomatically: false) ||
                _storage.Insert(to.Owner, item, out _, storageComp: to.Comp, playSound: false, stackAutomatically: false))
                continue;

            _container.TryRemoveFromContainer(item, true);
        }
    }
}
