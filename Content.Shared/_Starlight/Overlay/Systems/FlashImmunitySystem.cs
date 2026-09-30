using Content.Shared.Flash.Components;
using Content.Shared.Inventory;

namespace Content.Shared._Starlight.Overlay.Systems;
public sealed partial class FlashImmunitySystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;

    public bool HasFlashImmunityVisionBlockers(EntityUid uid)
    {
        if (TryComp(uid, out FlashImmunityComponent? flashImmunityComponent))
        {
            if (flashImmunityComponent.BlocksSpecialVision)
                return true;
        }

        if (TryComp<InventoryComponent>(uid, out var inventoryComp))
        {
            //get all worn items
            var slots = _inventory.GetSlotEnumerator((uid, inventoryComp), SlotFlags.WITHOUT_POCKET);
            while (slots.MoveNext(out var slot))
            {
                if (slot.ContainedEntity != null && TryComp(slot.ContainedEntity, out FlashImmunityComponent? wornFlashImmunityComponent))
                {
                    if (wornFlashImmunityComponent.BlocksSpecialVision)
                        return true;
                }
            }
        }

        return false;
    }
}
