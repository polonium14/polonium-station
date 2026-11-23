using Content.Shared._Polonium.Disposal;
using Content.Shared.Database;
using Content.Shared.Disposal.Components;
using Content.Shared.Humanoid;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;

namespace Content.Shared.Disposal.Unit;

public abstract partial class SharedDisposalUnitSystem
{
    [Dependency] private MobStateSystem _mobState = default!;

    private const float SmallCreatureMaxMass = 32f;

    private static readonly LocId FullPopup = "disposal-unit-is-full";
    private static readonly LocId HandBusyPopup = "polonium-disposal-hand-busy";
    private static readonly LocId CannotInsertPopup = "polonium-disposal-cannot-insert";
    private static readonly LocId CannotTakePopup = "polonium-disposal-cannot-take";
    private static readonly LocId SmallCreaturePopup = "polonium-disposal-small-creature";
    private static readonly LocId LargeCreaturePopup = "polonium-disposal-large-creature";

    /// <summary>
    /// Whether the entity is a living thing small enough to be seen dropping into the unit: at most 32 kg and not a playable species.
    /// </summary>
    public bool IsSmallCreature(EntityUid uid)
    {
        return IsSmallBody(uid) && !_mobState.IsDead(uid);
    }

    private bool IsSmallBody(EntityUid uid)
    {
        return HasComp<MobStateComponent>(uid)
               && !HasComp<HumanoidProfileComponent>(uid)
               && TryComp(uid, out PhysicsComponent? physics)
               && physics.FixturesMass <= SmallCreatureMaxMass;
    }

    [SubscribeLocalEvent]
    private void OnMouthPressed(Entity<DisposalUnitComponent> ent, ref DisposalDeviceMouthMessage args)
    {
        if (ent.Comp.Container is not { } container)
            return;

        if (_hands.GetActiveItem(args.Actor) is { } held)
            InsertHeld(ent, container, held, args.Actor);
        else
            TakeLast(ent, container, args.Actor);
    }

    private void InsertHeld(Entity<DisposalUnitComponent> ent, Container container, EntityUid held, EntityUid user)
    {
        if (HasComp<VirtualItemComponent>(held))
        {
            _popup.PopupCursor(Loc.GetString(HandBusyPopup), user);
            return;
        }

        if (container.Count >= ent.Comp.MaxCapacity)
        {
            _popup.PopupCursor(Loc.GetString(FullPopup), user);
            return;
        }

        if (!_container.CanInsert(held, container)
            || !_hands.TryDropIntoContainer(user, held, container))
        {
            _popup.PopupCursor(Loc.GetString(CannotInsertPopup), user);
            return;
        }

        Insert(ent, held, user);
    }

    private void TakeLast(Entity<DisposalUnitComponent> ent, Container container, EntityUid user)
    {
        var contents = container.ContainedEntities;
        if (contents.Count == 0)
            return;

        var last = contents[contents.Count - 1];
        if (last == user)
            return;

        if (!HasComp<ItemComponent>(last))
        {
            var popup = !IsSmallBody(last) ? LargeCreaturePopup : _mobState.IsDead(last) ? CannotTakePopup : SmallCreaturePopup;
            _popup.PopupCursor(Loc.GetString(popup), user);
            return;
        }

        if (!_hands.TryPickup(user, last))
        {
            // A two-handed item has already told the player how many hands it needs.
            if (!TryComp(last, out MultiHandedItemComponent? multiHanded) || _hands.CountFreeHands(user) >= multiHanded.HandsNeeded)
                _popup.PopupCursor(Loc.GetString(CannotTakePopup), user);

            return;
        }

        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(user):player} took {ToPrettyString(last)} out of {ToPrettyString(ent)}");
        RecalculateFlushTime(ent);
        UpdateVisualState(ent);
    }

    [SubscribeLocalEvent]
    private void OnMailingMessageAttempt(Entity<MailingUnitComponent> ent, ref BoundUserInterfaceMessageAttempt args)
    {
        if (args.Message is TargetSelectedMessage && !_power.IsPowered(ent.Owner))
            args.Cancel();
    }
}
