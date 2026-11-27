using Content.Shared.Body;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs.Components;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Robust.Shared.Containers;

// The upstream system's namespace, so this stays one partial class with it.
namespace Content.Shared.Medical.HealthAnalyzer;

/// <summary>
/// Polonium additions to the upstream analyzer. The patient scan reads the body, so only a mob with one can be scanned.
/// A health analyzer, or a PDA with the MedTek cartridge, keeps scanning when put away where a medic keeps it at hand:
/// a pocket, the PDA slot, the belt slot or a storage belt worn there. A backpack, the floor, a locker or someone
/// else's hands still stop it.
/// </summary>
public abstract partial class HealthAnalyzerSystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    private const SlotFlags ScanningSlots = SlotFlags.POCKET | SlotFlags.IDCARD | SlotFlags.BELT;

    private bool ValidScanTarget(EntityUid target)
    {
        return HasComp<MobStateComponent>(target) && HasComp<BodyComponent>(target);
    }

    private bool KeepsScanningWhereCarried(EntityUid analyzer)
    {
        return InHandOrScanningSlot(analyzer) || InWornBelt(analyzer, out _);
    }

    /// <summary>
    /// Inside a medical belt or another storage worn in the belt slot; <paramref name="wearer"/> wears the belt.
    /// </summary>
    private bool InWornBelt(EntityUid analyzer, out EntityUid wearer)
    {
        wearer = default;
        if (!_container.TryGetContainingContainer(analyzer, out var container)
            || !HasComp<StorageComponent>(container.Owner)
            || !_inventory.TryGetContainingSlot(container.Owner, out var slot)
            || (slot.SlotFlags & SlotFlags.BELT) == 0)
            return false;

        wearer = Transform(container.Owner).ParentUid;
        return true;
    }

    /// <summary>
    /// The window of an item in storage counts as out of reach once the storage is closed, which would shut the
    /// analyzer's window as soon as the medic closes their belt. The wearer can still reach a scanning analyzer there.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnAccessible(Entity<HealthAnalyzerComponent> analyzer, ref AccessibleOverrideEvent args)
    {
        if (args.Handled
            || analyzer.Comp.ScannedEntity == null
            || !InWornBelt(analyzer, out var wearer)
            || wearer != args.User)
            return;

        args.Handled = true;
        args.Accessible = true;
    }

    private bool InHandOrScanningSlot(EntityUid item)
    {
        if (!_container.TryGetContainingContainer(item, out var container))
            return false;

        if (_hands.IsHolding(container.Owner, item))
            return true;

        return _inventory.TryGetContainingSlot(item, out var slot) && (slot.SlotFlags & ScanningSlots) != 0;
    }
}
