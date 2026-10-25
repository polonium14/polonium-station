using Content.Shared._Polonium.Modules.ModSuit.Components;
using Content.Shared.Administration.Logs;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Content.Shared.Strip;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Utility;

namespace Content.Shared._Polonium.Modules.ModSuit;

public sealed partial class ModSuitCellSlotSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStrippableSystem _strippable = default!;

    private static readonly SpriteSpecifier EjectIcon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/eject.svg.192dpi.png"));

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModSuitCellSlotComponent, ItemSlotInsertAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<ModSuitCellSlotComponent, ItemSlotEjectAttemptEvent>(OnEjectAttempt);
        SubscribeLocalEvent<ModSuitCellSlotComponent, InteractUsingEvent>(OnInteractUsing, before: [typeof(ItemSlotsSystem)]);
        SubscribeLocalEvent<ModSuitCellSlotComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<ModSuitCellSlotComponent, InventoryRelayedEvent<GetVerbsEvent<EquipmentVerb>>>(OnGetEquipmentVerbs);
        SubscribeLocalEvent<ModSuitCellSlotComponent, ModSuitCellInsertDoAfterEvent>(OnInsertDoAfter);
        SubscribeLocalEvent<ModSuitCellSlotComponent, ModSuitCellEjectDoAfterEvent>(OnEjectDoAfter);
    }

    private void OnInsertAttempt(Entity<ModSuitCellSlotComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (!ent.Comp.AllowSlotChange && IsCellSlot(ent.Owner, args.Slot))
            args.Cancelled = true;
    }

    private void OnEjectAttempt(Entity<ModSuitCellSlotComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        if (!ent.Comp.AllowSlotChange && IsCellSlot(ent.Owner, args.Slot))
            args.Cancelled = true;
    }

    private void OnInteractUsing(Entity<ModSuitCellSlotComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<PowerCellComponent>(args.Used))
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, ent.Comp.SwapDelay, new ModSuitCellInsertDoAfterEvent(), ent.Owner, ent.Owner, args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
        };

        args.Handled = _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnGetVerbs(Entity<ModSuitCellSlotComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null || GetCell(ent.Owner) is not { } cell)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("modsuit-cell-eject-verb"),
            Icon = EjectIcon,
            Act = () => StartEject(ent, user, cell, ent.Comp.SwapDelay),
        });
    }

    private void OnGetEquipmentVerbs(Entity<ModSuitCellSlotComponent> ent, ref InventoryRelayedEvent<GetVerbsEvent<EquipmentVerb>> args)
    {
        var verbArgs = args.Args;
        var wearer = args.Owner;

        // The wearer uses the suit's own verbs, this one is for pulling the cell out of someone else.
        if (verbArgs.User == wearer || !verbArgs.CanAccess || !verbArgs.CanInteract || verbArgs.Hands == null)
            return;

        if (GetCell(ent.Owner) is not { } cell || !_inventory.TryGetContainingSlot(ent.Owner, out var slot))
            return;

        var user = verbArgs.User;
        verbArgs.Verbs.Add(new EquipmentVerb
        {
            Text = Loc.GetString("modsuit-cell-eject-other-verb", ("suit", ent.Owner)),
            Icon = EjectIcon,
            Act = () => StartEjectFromWearer(ent, user, wearer, cell, slot.StripTime),
        });
    }

    private void StartEject(Entity<ModSuitCellSlotComponent> ent, EntityUid user, EntityUid cell, TimeSpan delay, bool hidden = false)
    {
        var doAfterArgs = new DoAfterArgs(EntityManager, user, delay, new ModSuitCellEjectDoAfterEvent(), ent.Owner, ent.Owner, cell)
        {
            Hidden = hidden,
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void StartEjectFromWearer(Entity<ModSuitCellSlotComponent> ent, EntityUid user, EntityUid wearer, EntityUid cell, TimeSpan stripTime)
    {
        var (time, stealth) = _strippable.GetStripTimeModifiers(user, wearer, cell, stripTime);

        if (!stealth)
        {
            _popup.PopupEntity(
                Loc.GetString("modsuit-cell-alert-owner", ("user", Identity.Entity(user, EntityManager)), ("suit", ent.Owner)),
                wearer,
                wearer,
                PopupType.Large);
        }

        _adminLog.Add(LogType.Stripping,
            LogImpact.Low,
            $"{ToPrettyString(user):actor} is trying to remove the cell {ToPrettyString(cell):item} from {ToPrettyString(wearer):target}'s {ToPrettyString(ent.Owner)}");

        StartEject(ent, user, cell, time, stealth);
    }

    private void OnInsertDoAfter(Entity<ModSuitCellSlotComponent> ent, ref ModSuitCellInsertDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Used is not { } newCell || GetCellSlot(ent.Owner) is not { } slot)
            return;

        ent.Comp.AllowSlotChange = true;

        if (_itemSlots.CanInsert(ent.Owner, slot, newCell, args.User, swap: true))
        {
            EntityUid? oldCell = null;
            if (slot.HasItem && _itemSlots.TryEject(ent.Owner, slot, args.User, out var ejected, true))
                oldCell = ejected;

            _itemSlots.TryInsertFromHand(ent.Owner, slot, args.User, true);

            if (oldCell != null)
                _hands.PickupOrDrop(args.User, oldCell.Value);
        }

        ent.Comp.AllowSlotChange = false;
        args.Handled = true;
    }

    private void OnEjectDoAfter(Entity<ModSuitCellSlotComponent> ent, ref ModSuitCellEjectDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || GetCellSlot(ent.Owner) is not { } slot || slot.Item != args.Used)
            return;

        ent.Comp.AllowSlotChange = true;
        var ejected = _itemSlots.TryEjectToHands(ent.Owner, slot, args.User, true);
        ent.Comp.AllowSlotChange = false;

        if (ejected && _container.TryGetContainingContainer((ent.Owner, null, null), out var container) && container.Owner != args.User)
        {
            _adminLog.Add(LogType.Stripping,
                LogImpact.Medium,
                $"{ToPrettyString(args.User):actor} removed the cell {ToPrettyString(args.Used):item} from {ToPrettyString(container.Owner):target}'s {ToPrettyString(ent.Owner)}");
        }

        args.Handled = true;
    }

    private bool IsCellSlot(EntityUid uid, ItemSlot slot)
    {
        return GetCellSlot(uid) == slot;
    }

    private ItemSlot? GetCellSlot(EntityUid uid)
    {
        if (!TryComp<PowerCellSlotComponent>(uid, out var cellSlot))
            return null;

        return _itemSlots.TryGetSlot(uid, cellSlot.CellSlotId, out var slot) ? slot : null;
    }

    private EntityUid? GetCell(EntityUid uid)
    {
        return GetCellSlot(uid)?.Item;
    }
}
