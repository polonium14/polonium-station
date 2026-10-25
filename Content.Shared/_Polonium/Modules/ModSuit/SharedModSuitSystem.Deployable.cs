using Content.Shared.Clothing;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared._Polonium.Modules.Events;
using Content.Shared._Polonium.Modules.ModSuit.Components;
using Content.Shared._Polonium.Modules.ModSuit.Events;
using Robust.Shared.Containers;
using JetBrains.Annotations;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Item;

namespace Content.Shared._Polonium.Modules.ModSuit;

public partial class SharedModSuitSystem
{
    [Dependency] protected SharedContainerSystem Container = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedItemSystem _item = default!;

    private const string DeployableSlotPrefix = "deployable-";

    private void InitializeDeployable()
    {
        InitializeDeployableRelay();

        SubscribeLocalEvent<ModSuitPartDeployableComponent, ComponentInit>(OnDeployableInit);
        SubscribeLocalEvent<ModSuitPartDeployableComponent, ComponentRemove>(OnDeployableRemoved);
        SubscribeLocalEvent<ModSuitPartDeployableComponent, ClothingGotEquippedEvent>(OnDeployableEquipped);
        SubscribeLocalEvent<ModSuitPartDeployableComponent, ClothingGotUnequippedEvent>(OnDeployableUnequipped);
        SubscribeLocalEvent<ModSuitPartDeployableComponent, ModuleGetUserEvent>(OnDeployableGetUser);

        SubscribeLocalEvent<ModSuitPartDeployableComponent, ModuleAddedContainerEvent>(OnDeployableModuleAdded);
        SubscribeLocalEvent<ModSuitPartDeployableComponent, ModuleRemovedContainerEvent>(OnDeployableModuleRemoved);

        SubscribeLocalEvent<ModSuitDeployedPartComponent, BeingUnequippedAttemptEvent>(OnDeployedUnequipAttempt);

        SubscribeLocalEvent<ModSuitDeployableInventoryComponent, ComponentInit>(OnDeployableInventoryInit);
        SubscribeLocalEvent<ModSuitDeployableInventoryComponent, ComponentRemove>(OnDeployableInventoryRemoved);
        SubscribeLocalEvent<ModSuitDeployableInventoryComponent, ModSuitDeployablePartBeforeDeployedEvent>(OnDeployableInventoryBeforeDeployed);
        SubscribeLocalEvent<ModSuitDeployableInventoryComponent, ModSuitDeployablePartUndeployedEvent>(OnDeployableInventoryUndeployed);
    }

    #region Deployable

    protected abstract void OnDeployableInit(Entity<ModSuitPartDeployableComponent> ent, ref ComponentInit args);

    private void OnDeployableRemoved(Entity<ModSuitPartDeployableComponent> ent, ref ComponentRemove args)
    {
        if (ent.Comp.Wearer != null)
            UndeployAll(ent, ent.Comp.Wearer.Value);

        var i = 1;
        foreach (var (slot, part) in ent.Comp.DeployedParts)
        {
            var partEv = new ModSuitDeployablePartUndeployedEvent(ent.Owner, ent.Comp.Wearer, slot, i);
            RaiseLocalEvent(part, partEv);

            var suitEv = new ModSuitPartDeployableUndeployedEvent(part, ent.Comp.Wearer, slot, i++);
            RaiseLocalEvent(ent.Owner, suitEv);

            if (_net.IsServer)
                Del(part);
        }

        foreach (var (_, container) in ent.Comp.DeployableContainers)
            Container.ShutdownContainer(container);
    }

    private void OnDeployableEquipped(Entity<ModSuitPartDeployableComponent> ent, ref ClothingGotEquippedEvent args)
    {
        ent.Comp.Wearer = args.Wearer;

        var inventoryComp = CompOrNull<InventoryComponent>(args.Wearer);

        // cleanup any mess we forgot to do
        UndeployAll(ent, (args.Wearer, inventoryComp));

        if (inventoryComp == null)
        {
            Dirty(ent);
            return;
        }

        var i = 1;
        foreach (var (slot, container) in ent.Comp.DeployableContainers)
        {
            if (!_inventory.HasSlot(args.Wearer, slot, inventoryComp))
                continue;

            if (container.ContainedEntity is not { } part)
                continue;

            var beforeEv = new ModSuitDeployablePartBeforeDeployedEvent(ent.Owner, args.Wearer, slot, i++);
            RaiseLocalEvent(part, beforeEv);

            if (!_inventory.TryEquip(args.Wearer, part, slot, true, true, true, inventoryComp))
            {
                var failedPartEv = new ModSuitDeployablePartUndeployedEvent(ent.Owner, args.Wearer, slot, 0);
                RaiseLocalEvent(part, failedPartEv);

                var failedSuitEv = new ModSuitPartDeployableUndeployedEvent(part, args.Wearer, slot, 0);
                RaiseLocalEvent(ent.Owner, failedSuitEv);

                Container.Insert(part, container);
                continue;
            }

            ent.Comp.DeployedParts[slot] = part;

            var afterPartEv = new ModSuitDeployablePartDeployedEvent(ent.Owner, args.Wearer, slot, i);
            RaiseLocalEvent(part, afterPartEv);

            var afterSuitEv = new ModSuitPartDeployableDeployedEvent(part, args.Wearer, slot, i);
            RaiseLocalEvent(ent.Owner, afterSuitEv);
        }

        Dirty(ent);
    }

    private void OnDeployableUnequipped(Entity<ModSuitPartDeployableComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        UndeployAll(ent, args.Wearer);
        ent.Comp.Wearer = null;
        Dirty(ent);
    }

    private void OnDeployableGetUser(Entity<ModSuitPartDeployableComponent> ent, ref ModuleGetUserEvent args)
    {
        args.User ??= ent.Comp.Wearer;
    }

    private void OnDeployableModuleAdded(Entity<ModSuitPartDeployableComponent> ent, ref ModuleAddedContainerEvent args)
    {
        _item.VisualsChanged(ent.Owner);

        foreach (var part in GetAllParts(ent!))
            _item.VisualsChanged(part);
    }

    private void OnDeployableModuleRemoved(Entity<ModSuitPartDeployableComponent> ent, ref ModuleRemovedContainerEvent args)
    {
        _item.VisualsChanged(ent.Owner);

        foreach (var part in GetAllParts(ent!))
            _item.VisualsChanged(part);
    }

    private void OnDeployedUnequipAttempt(Entity<ModSuitDeployedPartComponent> ent, ref BeingUnequippedAttemptEvent args)
    {
        args.Cancel();
    }

    private void UndeployAll(Entity<ModSuitPartDeployableComponent> ent, Entity<InventoryComponent?> wearer)
    {
        var i = 1;
        foreach (var (slot, part) in ent.Comp.DeployedParts)
        {
            if (_inventory.TryUnequip(wearer.Owner, slot, true, true, true, wearer.Comp))
            {
                var container = ent.Comp.DeployableContainers[slot];
                Container.Insert(part, container);
            }

            var partEv = new ModSuitDeployablePartUndeployedEvent(ent.Owner, wearer.Owner, slot, i);
            RaiseLocalEvent(part, partEv);

            var suitEv = new ModSuitPartDeployableUndeployedEvent(part, wearer.Owner, slot, i++);
            RaiseLocalEvent(ent.Owner, suitEv);
        }

        ent.Comp.DeployedParts.Clear();
    }

    protected static string GetDeployableSlotId(string slot)
    {
        return DeployableSlotPrefix + slot;
    }

    [PublicAPI]
    public IEnumerable<EntityUid> GetDeployedParts(Entity<ModSuitPartDeployableComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            yield break;

        foreach (var (_, part) in ent.Comp.DeployedParts)
            yield return part;
    }

    [PublicAPI]
    public IEnumerable<EntityUid> GetDeployableParts(Entity<ModSuitPartDeployableComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
            yield break;

        foreach (var (_, container) in ent.Comp.DeployableContainers)
        {
            if (container.ContainedEntity != null)
                yield return container.ContainedEntity.Value;
        }
    }

    [PublicAPI]
    public IEnumerable<EntityUid> GetAllParts(Entity<ModSuitPartDeployableComponent?> ent)
    {
        return GetDeployedParts(ent).Concat(GetDeployableParts(ent));
    }

    [PublicAPI]
    public bool TryGetDeployedPart(Entity<ModSuitPartDeployableComponent?> ent, ModSuitPartType type, [NotNullWhen(true)] out EntityUid? foundPart)
    {
        var parts = GetDeployedParts(ent);
        parts = parts.Append(ent.Owner);
        return TryGetPart(parts, type, out foundPart);
    }

    [PublicAPI]
    public bool TryGetDeployablePart(Entity<ModSuitPartDeployableComponent?> ent, ModSuitPartType type, [NotNullWhen(true)] out EntityUid? foundPart)
    {
        var parts = GetDeployableParts(ent);
        parts = parts.Append(ent.Owner);
        return TryGetPart(parts, type, out foundPart);
    }

    private bool TryGetPart(IEnumerable<EntityUid> parts, ModSuitPartType type, [NotNullWhen(true)] out EntityUid? foundPart)
    {
        foreach (var part in parts)
        {
            if (!TryComp<ModSuitPartTypeComponent>(part, out var typeComp))
                continue;

            if (typeComp.Type != type)
                continue;

            foundPart = part;
            return true;
        }

        foundPart = null;
        return false;
    }

    #endregion

    #region Inventory

    private void OnDeployableInventoryInit(Entity<ModSuitDeployableInventoryComponent> ent, ref ComponentInit args)
    {
        ent.Comp.StoredItem = Container.EnsureContainer<ContainerSlot>(ent.Owner, ent.Comp.ContainerId);
    }

    private void OnDeployableInventoryRemoved(Entity<ModSuitDeployableInventoryComponent> ent, ref ComponentRemove args)
    {
        Container.ShutdownContainer(ent.Comp.StoredItem);
    }

    private void OnDeployableInventoryBeforeDeployed(Entity<ModSuitDeployableInventoryComponent> ent, ref ModSuitDeployablePartBeforeDeployedEvent args)
    {
        Container.EmptyContainer(ent.Comp.StoredItem, true);

        if (args.Wearer is not { } wearer)
            return;

        if (!_inventory.TryGetSlotEntity(wearer, args.Slot, out var slotEntity))
            return;

        if (TerminatingOrDeleted(slotEntity))
            return;

        _inventory.TryUnequip(wearer, args.Slot, true, true, true);
        Container.Insert(slotEntity.Value, ent.Comp.StoredItem);
    }

    private void OnDeployableInventoryUndeployed(Entity<ModSuitDeployableInventoryComponent> ent, ref ModSuitDeployablePartUndeployedEvent args)
    {
        if (ent.Comp.StoredItem?.ContainedEntity is not { } containedEntity)
            return;

        if (TerminatingOrDeleted(containedEntity))
            return;

        if (args.Wearer is not { } wearer)
        {
            Container.Remove(containedEntity, ent.Comp.StoredItem);
            return;
        }

        _inventory.TryEquip(wearer, containedEntity, args.Slot, true, true, true);
    }

    #endregion
}
