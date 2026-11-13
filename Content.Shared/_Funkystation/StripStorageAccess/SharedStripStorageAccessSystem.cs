using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Utility;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Strip;
using Content.Shared.Strip.Components;

namespace Content.Shared._Funkystation.StripStorageAccess;

/// <summary>
/// lets someone open a person's worn storage from the strip menu via interact
/// </summary>
public sealed partial class SharedStripStorageAccessSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = null!;
    [Dependency] private SharedStorageSystem _storage = null!;
    [Dependency] private InventorySystem _inventory = null!;
    [Dependency] private SharedHandsSystem _hands = null!;
    [Dependency] private SharedPopupSystem _popup = null!;
    [Dependency] private SharedAudioSystem _audio = null!;
    [Dependency] private SharedContainerSystem _container = null!;
    [Dependency] private SharedInteractionSystem _interaction = null!;
    [Dependency] private SharedStrippableSystem _strippable = null!;
    [Dependency] private SharedUserInterfaceSystem _ui = null!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StrippableComponent, StrippingOpenStorageButtonPressed>(OnOpenStorageButtonPressed);

        SubscribeLocalEvent<StorageComponent, StripStorageOpenDoAfterEvent>(OnOpenDoAfter);
        SubscribeLocalEvent<StorageComponent, StripStorageRemoveDoAfterEvent>(OnRemoveDoAfter);
        SubscribeLocalEvent<StorageComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerbs);
    }

    private void OnOpenStorageButtonPressed(Entity<StrippableComponent> strippable, ref StrippingOpenStorageButtonPressed args)
    {
        if (args.Actor is not { Valid: true } user || !TryComp<HandsComponent>(user, out _))
            return;

        EntityUid? slotEntity;
        if (args.IsHand)
        {
            slotEntity = _hands.GetHeldItem(strippable.Owner, args.Slot);
        }
        else
        {
            // pockets are obscured in the strip menu, so don't let their contents be opened either
            if (!_inventory.TryGetSlotContainer(strippable, args.Slot, out var slot, out var slotDef) ||
                _strippable.IsStripHidden(slotDef, user))
            {
                return;
            }

            slotEntity = slot.ContainedEntity;
        }

        if (slotEntity is not { } storageUid || !HasComp<StorageComponent>(storageUid))
        {
            return;
        }

        StartOpenStorageDoAfter(user, storageUid, strippable.Owner);
    }

    private void OnGetVerbs(Entity<StorageComponent> target, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (args.Hands == null || !args.CanInteract || args.Target == args.User)
            return;

        if (!_container.TryGetContainingContainer(target.Owner, out var container) || container.Owner == args.User)
            return;

        if (!HasComp<InventoryComponent>(container.Owner) && !HasComp<HandsComponent>(container.Owner))
            return;

        var user = args.User;
        var holder = container.Owner;
        var storageUid = target.Owner;

        if (!_interaction.InRangeUnobstructed(user, holder))
            return;

        if (_inventory.TryGetContainingSlot(storageUid, out var slotDef) && _strippable.IsStripHidden(slotDef, user))
            return;

        InteractionVerb verb = new()
        {
            Text = Loc.GetString("strip-storage-access-verb-text"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/open.svg.192dpi.png")),
            Act = () => StartOpenStorageDoAfter(user, storageUid, holder),
        };

        args.Verbs.Add(verb);
    }

    private void StartOpenStorageDoAfter(EntityUid user, EntityUid storageUid, EntityUid holder)
    {
        var doAfterArgs = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(5), new StripStorageOpenDoAfterEvent(), storageUid, holder)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = SharedInteractionSystem.InteractionRange,
            NeedHand = true,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget,
        };

        var started = _doAfter.TryStartDoAfter(doAfterArgs);

        if (!started)
            return;

        _audio.PlayPredicted(new SoundCollectionSpecifier("storageRustle"), storageUid, user);

        _popup.PopupEntity(
            Loc.GetString("strip-storage-access-alert-target",
                ("user", Identity.Entity(user, EntityManager)),
                ("item", storageUid)),
            holder,
            holder,
            PopupType.Medium);
    }

    private void OnOpenDoAfter(Entity<StorageComponent> storage, ref StripStorageOpenDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        // the holder may have dropped or handed off the storage during the doafter
        if (args.Target is not { } holder ||
            !_container.TryGetContainingContainer(storage.Owner, out var container) ||
            container.Owner != holder)
            return;

        args.Handled = true;

        // SharedInteractionSystem.UiRangeCheck keeps the UI open while the holder stays in range
        _storage.OpenStorageUI(storage, args.User);
    }

    private void OnRemoveDoAfter(Entity<StorageComponent> storage, ref StripStorageRemoveDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (args.Used is not { } item ||
            !TryComp<HandsComponent>(args.User, out var hands) ||
            !TryComp<ItemComponent>(item, out var itemComp))
            return;

        // the holder may have moved the item or closed the bag during the doafter
        if (!storage.Comp.Container.Contains(item) ||
            !_ui.IsUiOpen(storage.Owner, StorageComponent.StorageUiKey.Key, args.User))
            return;

        args.Handled = true;

        _storage.DoInteractWithStoredItem((args.User, hands), storage, (item, itemComp));
    }
}
