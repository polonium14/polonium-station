using Content.Shared.Inventory;
using Robust.Shared.Containers;

namespace Content.Shared.SubFloor;

public abstract partial class SharedTRayScannerSystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private InventorySystem _inventory = default!;

    // The R.I.G. meson visor adds the scanner to an already worn helmet, so the equip events never fire for it.
    [SubscribeLocalEvent]
    private void OnWornScannerStartup(Entity<TRayScannerComponent> ent, ref ComponentStartup args)
    {
        if (GetWearer(ent.Owner) is { } wearer)
            OnEquip(wearer);
    }

    [SubscribeLocalEvent]
    private void OnWornScannerRemove(Entity<TRayScannerComponent> ent, ref ComponentRemove args)
    {
        if (TerminatingOrDeleted(ent.Owner))
            return;

        if (GetWearer(ent.Owner) is { } wearer)
            OnUnequip(wearer);
    }

    private EntityUid? GetWearer(EntityUid uid)
    {
        if (!_inventory.TryGetContainingSlot(uid, out _) || !_container.TryGetContainingContainer((uid, null, null), out var container))
            return null;

        return container.Owner;
    }
}
