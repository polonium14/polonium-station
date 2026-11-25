using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Chat;
using Content.Shared.Clothing.Components;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.MaskHailer;

public sealed partial class MaskHailerSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedChatSystem _chat = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    private const string BoundUserInterface = "MaskHailerBoundUserInterface";

    private static readonly LocId PulledDownPopup = "polonium-mask-hailer-pulled-down";

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<MaskHailerComponent> ent, ref MapInitEvent args)
    {
        _ui.SetUi(ent.Owner, MaskHailerUiKey.Key, new InterfaceData(BoundUserInterface));
    }

    [SubscribeLocalEvent]
    private void OnGetActions(Entity<MaskHailerComponent> ent, ref GetItemActionsEvent args)
    {
        if (!_inventory.InSlotWithFlags(ent.Owner, SlotFlags.MASK))
            return;

        args.AddAction(ref ent.Comp.ActionEntity, ent.Comp.Action);
        Dirty(ent);
    }

    [SubscribeLocalEvent]
    private void OnAction(Entity<MaskHailerComponent> ent, ref MaskHailerActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!IsWornBy(ent, args.Performer))
            return;

        if (IsPulledDown(ent))
        {
            _popup.PopupEntity(Loc.GetString(PulledDownPopup), args.Performer, args.Performer);
            return;
        }

        _ui.OpenUi(ent.Owner, MaskHailerUiKey.Key, args.Performer);
    }

    [SubscribeLocalEvent]
    private void OnPhrase(Entity<MaskHailerComponent> ent, ref MaskHailerPhraseMessage args)
    {
        var user = args.Actor;

        if (!IsWornBy(ent, user)
            || IsPulledDown(ent)
            || !TryComp<ActionComponent>(ent.Comp.ActionEntity, out var action)
            || _actions.IsCooldownActive(action))
            return;

        if (!_prototype.Resolve(ent.Comp.Set, out var set))
            return;

        if (args.Index < 0
            || args.Index >= set.Phrases.Count
            || args.Variant < 0
            || args.Variant >= set.Phrases[args.Index].Variants.Count)
        {
            Log.Warning($"{ToPrettyString(user)} sent phrase {args.Index}/{args.Variant} outside of mask hailer set {set.ID}.");
            return;
        }

        var phrase = set.Phrases[args.Index].Variants[args.Variant];

        _actions.SetCooldown(ent.Comp.ActionEntity, ent.Comp.Cooldown);
        _audio.PlayPredicted(phrase.Sound, user, user);
        _chat.TrySendInGameICMessage(user,
            Loc.GetString(phrase.Text),
            InGameICChatType.Speak,
            ChatTransmitRange.Normal,
            checkRadioPrefix: false,
            ignoreActionBlocker: true);
        _ui.CloseUi(ent.Owner, MaskHailerUiKey.Key, user);
    }

    private bool IsWornBy(EntityUid mask, EntityUid user)
    {
        return _inventory.InSlotWithFlags(mask, SlotFlags.MASK) && Transform(mask).ParentUid == user;
    }

    private bool IsPulledDown(EntityUid mask)
    {
        return TryComp<MaskComponent>(mask, out var comp) && comp.IsToggled;
    }
}
