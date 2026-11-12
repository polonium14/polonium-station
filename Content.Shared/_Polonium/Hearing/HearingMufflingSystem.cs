using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle;

namespace Content.Shared._Polonium.Hearing;

public sealed partial class HearingMufflingSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;

    [Dependency] private EntityQuery<HearingMufflingComponent> _mufflingQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HearingMufflingComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<HearingMufflingComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(ent.Comp.ExamineText));
    }

    /// <summary>
    /// Whether the listener wears something switched on that keeps them from hearing the world.
    /// </summary>
    public bool IsHearingMuffled(EntityUid listener)
    {
        return TryGetMuffling(listener, out _);
    }

    /// <summary>
    /// Finds the switched-on item that muffles the listener's hearing, if they wear one.
    /// </summary>
    public bool TryGetMuffling(EntityUid listener, out Entity<HearingMufflingComponent> muffling)
    {
        muffling = default;

        if (!_inventory.TryGetContainerSlotEnumerator(listener, out var enumerator, SlotFlags.HEAD | SlotFlags.EARS))
            return false;

        while (enumerator.NextItem(out var item, out var slot))
        {
            if (!_mufflingQuery.TryComp(item, out var comp) || (slot.SlotFlags & comp.Slots) == 0)
                continue;

            if (!_toggle.IsActivated(item))
                continue;

            muffling = (item, comp);
            return true;
        }

        return false;
    }
}
