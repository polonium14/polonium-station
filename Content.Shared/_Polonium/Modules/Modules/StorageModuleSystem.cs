using System.Linq;
using Content.Shared._Polonium.Modules.Components.Modules;
using Content.Shared._Polonium.Modules.Events;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Shared._Polonium.Modules.Modules;

public sealed partial class StorageModuleSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StorageModuleComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<StorageModuleComponent, ComponentRemove>(OnRemove);

        SubscribeLocalEvent<StorageModuleComponent, ModuleAddedContainerEvent>(OnAdded);
        SubscribeLocalEvent<StorageModuleComponent, ModuleRemovedContainerEvent>(OnRemoved);

        SubscribeLocalEvent<StorageModuleComponent, StorageInteractUsingAttemptEvent>(OnStorageUsingAttempt);
        SubscribeLocalEvent<StorageModuleComponent, StorageInteractAttemptEvent>(OnStorageAttempt);
    }

    private void OnInit(Entity<StorageModuleComponent> ent, ref ComponentInit args)
    {
        ent.Comp.Items = _container.EnsureContainer<Container>(ent.Owner, StorageModuleComponent.ContainerId);
    }

    private void OnRemove(Entity<StorageModuleComponent> ent, ref ComponentRemove args)
    {
        _container.ShutdownContainer(ent.Comp.Items);
    }

    private void OnAdded(Entity<StorageModuleComponent> ent, ref ModuleAddedContainerEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (!TryComp<StorageComponent>(ent.Owner, out var moduleStorage))
            return;

        if (!TryComp<StorageComponent>(args.Container, out var containerStorage))
        {
            _storage.CopyComponent((ent.Owner, moduleStorage), args.Container);
            containerStorage = Comp<StorageComponent>(args.Container);
        }

        var items = ent.Comp.Items.ContainedEntities.ToArray();
        foreach (var item in items)
        {
            var location = ent.Comp.Locations[item];
            _storage.InsertAt((args.Container, containerStorage), item, location, out _, null, false, false);
        }
    }

    private void OnRemoved(Entity<StorageModuleComponent> ent, ref ModuleRemovedContainerEvent args)
    {
        if (TerminatingOrDeleted(ent.Owner) || TerminatingOrDeleted(args.Container))
            return;

        if (!TryComp<StorageComponent>(args.Container, out var containerStorage))
            return;

        ent.Comp.Locations.Clear();
        foreach (var (item, location) in containerStorage.StoredItems)
        {
            if (_container.Insert(item, ent.Comp.Items))
                ent.Comp.Locations[item] = location;
        }

        Dirty(ent);
        RemCompDeferred(args.Container, containerStorage);
    }

    private void OnStorageUsingAttempt(Entity<StorageModuleComponent> ent, ref StorageInteractUsingAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnStorageAttempt(Entity<StorageModuleComponent> ent, ref StorageInteractAttemptEvent args)
    {
        args.Silent = true;
        args.Cancelled = true;
    }

}
