using Content.Shared._Funkystation.Clothing.Components;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle.Components;
using Robust.Shared.Containers;

namespace Content.Shared.SubFloor;

public abstract partial class SharedTRayScannerSystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private InventorySystem _inventory = default!;

    /// <summary>
    /// Whether the scanner currently reveals subfloor entities.
    /// </summary>
    public bool IsScannerActive(Entity<TRayScannerComponent> ent)
    {
        return !ent.Comp.RequireToggle || _itemToggle.IsActivated(ent.Owner);
    }

    [SubscribeLocalEvent]
    private void OnScannerToggled(Entity<TRayScannerComponent> ent, ref ItemToggledEvent args)
    {
        if (!ent.Comp.RequireToggle || !TryComp<GoggleShaderComponent>(ent, out var goggleShader))
            return;

        goggleShader.Enabled = args.Activated;
        Dirty(ent, goggleShader);

        var ev = new GoggleShaderToggledEvent(args.Activated);
        RaiseLocalEvent(ent, ref ev);
    }

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
