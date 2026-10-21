using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle;

namespace Content.Shared._Polonium.Hearing;

public sealed partial class SpeechMufflingSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;

    [Dependency] private EntityQuery<SpeechMufflingComponent> _mufflingQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpeechMufflingComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<SpeechMufflingComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(ent.Comp.ExamineText));
    }

    /// <summary>
    /// Whether the listener wears something switched on that keeps them from hearing speech.
    /// </summary>
    public bool IsSpeechMuffled(EntityUid listener)
    {
        if (!_inventory.TryGetContainerSlotEnumerator(listener, out var enumerator, SlotFlags.HEAD | SlotFlags.EARS))
            return false;

        while (enumerator.NextItem(out var item, out var slot))
        {
            if (!_mufflingQuery.TryComp(item, out var muffling) || (slot.SlotFlags & muffling.Slots) == 0)
                continue;

            if (_toggle.IsActivated(item))
                return true;
        }

        return false;
    }
}
