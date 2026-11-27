// Ported from Goob-Station (https://github.com/Goob-Station/Goob-Station/pull/4929), AGPL-3.0-or-later.

using Content.Shared.Administration.Logs;
using Content.Shared.Chat;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.EntityTable;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Stacks;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Polonium.SlotMachine;

public sealed partial class SlotMachineSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private EntityTableSystem _entityTable = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedChatSystem _chat = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private SharedStackSystem _stack = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SlotMachineComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<SlotMachineComponent, SlotMachineDoAfterEvent>(OnSpinDoAfter);
    }

    private void OnActivate(Entity<SlotMachineComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex || !_power.IsPowered(ent.Owner))
            return;

        args.Handled = true;

        if (ent.Comp.IsSpinning)
        {
            _popup.PopupEntity(Loc.GetString("slotmachine-busy"), ent.Owner, args.User);
            return;
        }

        if (!_itemSlots.TryGetSlot(ent.Owner, ent.Comp.MoneySlot, out var slot)
            || slot.Item is not { } money
            || !_stack.TryUse(money, ent.Comp.SpinCost))
        {
            _popup.PopupEntity(Loc.GetString("slotmachine-no-money"), ent.Owner, args.User);
            return;
        }

        ent.Comp.IsSpinning = true;
        ent.Comp.Spinner = args.User;
        Dirty(ent);

        // The machine itself is the do-after user, so the client cannot predict it like a player's do-after.
        // Running it on the client as well makes the progress bar jitter, so only the server starts it.
        if (_net.IsClient)
            return;

        var doAfter = new DoAfterArgs(EntityManager, ent.Owner, ent.Comp.SpinDuration, new SlotMachineDoAfterEvent(), ent.Owner)
        {
            BreakOnMove = false,
            BreakOnDamage = false,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
        {
            ent.Comp.IsSpinning = false;
            ent.Comp.Spinner = null;
            Dirty(ent);
            return;
        }

        _audio.PlayPvs(ent.Comp.SpinSound, ent.Owner);
        _appearance.SetData(ent.Owner, SlotMachineVisuals.Spinning, true);
    }

    private void OnSpinDoAfter(Entity<SlotMachineComponent> ent, ref SlotMachineDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var spinner = ent.Comp.Spinner;
        ent.Comp.IsSpinning = false;
        ent.Comp.Spinner = null;
        Dirty(ent);
        _appearance.SetData(ent.Owner, SlotMachineVisuals.Spinning, false);

        if (args.Cancelled)
            return;

        var prize = PickPrize(ent.Comp.Prizes);
        if (prize == null)
            return;

        if (prize.PrizeTable != null)
        {
            var coords = Transform(ent.Owner).Coordinates;
            foreach (var item in _entityTable.GetSpawns(prize.PrizeTable))
            {
                PredictedSpawnAtPosition(item, coords);
            }
        }

        _audio.PlayPvs(prize.WinSound, ent.Owner);

        if (prize.WinMessage is { } message)
        {
            _chat.TrySendInGameICMessage(ent.Owner,
                Loc.GetString(message),
                InGameICChatType.Speak,
                hideChat: false,
                hideLog: true,
                checkRadioPrefix: false);
        }

        if (spinner != null)
        {
            _adminLog.Add(LogType.Action,
                LogImpact.Low,
                $"{ToPrettyString(spinner.Value):user} spun {ToPrettyString(ent.Owner):machine} for {ent.Comp.SpinCost} spesos and rolled {prize.ID}");
        }
    }

    /// <summary>
    /// Picks one of the prizes at random, weighted by <see cref="SlotMachinePrizePrototype.Weight"/>.
    /// </summary>
    private SlotMachinePrizePrototype? PickPrize(List<ProtoId<SlotMachinePrizePrototype>> prizes)
    {
        var sum = 0f;
        foreach (var id in prizes)
        {
            sum += _proto.Index(id).Weight;
        }

        var roll = _random.NextFloat() * sum;
        SlotMachinePrizePrototype? last = null;
        foreach (var id in prizes)
        {
            last = _proto.Index(id);
            roll -= last.Weight;
            if (roll <= 0f)
                return last;
        }

        // Only reachable through float rounding at the very top of the range.
        return last;
    }
}
