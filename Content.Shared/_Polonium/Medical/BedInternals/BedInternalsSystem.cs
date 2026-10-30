using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._Polonium.Medical.BedInternals;

public sealed partial class BedInternalsSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedGasTankSystem _gasTank = default!;
    [Dependency] private SharedInternalsSystem _internals = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const string MaskSlot = "mask";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BedInternalsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BedInternalsComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerbs);
        SubscribeLocalEvent<BedInternalsComponent, EntInsertedIntoContainerMessage>(OnEntInserted);
        SubscribeLocalEvent<BedInternalsComponent, EntRemovedFromContainerMessage>(OnEntRemoved);
        SubscribeLocalEvent<BedInternalsComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<BedInternalsComponent, UnstrappedEvent>(OnUnstrapped);
    }

    private void OnMapInit(Entity<BedInternalsComponent> ent, ref MapInitEvent args)
    {
        UpdateTankVisual(ent);
    }

    private void OnGetVerbs(Entity<BedInternalsComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        // There is nobody to supply gas to unless someone is strapped in.
        if (!TryComp<StrapComponent>(ent, out var strap) || strap.BuckledEntities.Count == 0)
            return;

        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString(ent.Comp.Enabled ? "bed-internals-verb-disable" : "bed-internals-verb-enable"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/dot.svg.192dpi.png")),
            Act = () => Toggle(ent, user),
        });
    }

    private void OnEntInserted(Entity<BedInternalsComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == ent.Comp.GasSlot)
            UpdateTankVisual(ent);
    }

    private void OnEntRemoved(Entity<BedInternalsComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.GasSlot)
            return;

        UpdateTankVisual(ent);

        // While the client applies a server state the patient and the mask are already in their final place.
        if (ent.Comp.Enabled && !_timing.ApplyingState)
            Disable(ent);
    }

    private void OnStrapped(Entity<BedInternalsComponent> ent, ref StrappedEvent args)
    {
        if (!ent.Comp.Enabled || GetTank(ent) is not { } tank)
            return;

        TryApply(ent, tank, args.Buckle.Owner, null);
    }

    private void OnUnstrapped(Entity<BedInternalsComponent> ent, ref UnstrappedEvent args)
    {
        Release(ent, args.Buckle.Owner);

        // The last patient has left, so the next one starts with the internals off.
        if (ent.Comp.Enabled && args.Strap.Comp.BuckledEntities.Count == 0)
        {
            ent.Comp.Enabled = false;
            Dirty(ent);
        }
    }

    private void Toggle(Entity<BedInternalsComponent> ent, EntityUid user)
    {
        if (ent.Comp.Enabled)
            Disable(ent);
        else
            Enable(ent, user);
    }

    private void Enable(Entity<BedInternalsComponent> ent, EntityUid user)
    {
        if (!TryComp<StrapComponent>(ent, out var strap) || strap.BuckledEntities.Count == 0)
            return;

        if (GetTank(ent) is not { } tank)
        {
            _popup.PopupClient(Loc.GetString("bed-internals-no-tank"), ent, user, PopupType.SmallCaution);
            return;
        }

        var supplied = false;
        foreach (var patient in strap.BuckledEntities)
        {
            supplied |= TryApply(ent, tank, patient, user);
        }

        if (!supplied)
            return;

        ent.Comp.Enabled = true;
        Dirty(ent);
        _audio.PlayPredicted(ent.Comp.ToggleSound, ent, user);
    }

    private void Disable(Entity<BedInternalsComponent> ent)
    {
        ent.Comp.Enabled = false;
        Dirty(ent);

        if (!TryComp<StrapComponent>(ent, out var strap))
            return;

        foreach (var patient in strap.BuckledEntities)
        {
            Release(ent, patient);
        }
    }

    /// <summary>
    /// Connects the bed's tank to the patient, putting the bed's mask on them if they have no breath mask.
    /// </summary>
    /// <param name="user">Who asked for it, to show them why it did not work. Null when it happens on its own.</param>
    private bool TryApply(Entity<BedInternalsComponent> bed, Entity<GasTankComponent> tank, EntityUid patient, EntityUid? user)
    {
        if (HasComp<BedInternalsPatientComponent>(patient))
            return true;

        if (!TryComp<InternalsComponent>(patient, out var internals))
            return Fail("bed-internals-patient-no-internals", patient, user);

        if (tank.Comp.ReleaseValveOpen)
            return Fail("bed-internals-valve-open", patient, user);

        EntityUid? bedMask = null;
        if (_inventory.TryGetSlotEntity(patient, MaskSlot, out var worn))
        {
            // Some other mask covers the face, or the breath mask is pulled down.
            if (!HasComp<BreathToolComponent>(worn) || internals.BreathTools.Count == 0)
                return Fail("bed-internals-mask-blocked", patient, user);
        }
        else
        {
            if (GetBedMask(bed) is not { } mask)
                return Fail("bed-internals-no-mask", patient, user);

            if (!_inventory.TryEquip(patient, mask, MaskSlot, silent: true, force: true))
                return Fail("bed-internals-mask-blocked", patient, user);

            // Only now that it is on the patient: it must not leave the bed's container before.
            if (!HasComp<UnremoveableComponent>(mask))
                AddComp(mask, new UnremoveableComponent { DeleteOnDrop = false });

            bedMask = mask;
        }

        var state = AddComp<BedInternalsPatientComponent>(patient);
        state.BedTank = tank.Owner;
        state.BedMask = bedMask;
        if (_internals.AreInternalsWorking(internals) && internals.GasTankEntity != tank.Owner)
            state.PreviousTank = internals.GasTankEntity;
        Dirty(patient, state);

        if (internals.GasTankEntity != null)
            _internals.DisconnectTank((patient, internals), forced: true);

        // The tank sits in the bed, not on the patient, so it has to be told who uses it.
        tank.Comp.User = patient;
        tank.Comp.CheckUser = false;
        Dirty(tank);

        if (!_internals.TryConnectTank((patient, internals), tank.Owner))
        {
            tank.Comp.User = null;
            Dirty(tank);
            Release(bed, patient);
            return Fail("bed-internals-mask-blocked", patient, user);
        }

        _gasTank.UpdateUserInterface(tank);
        return true;
    }

    /// <summary>
    /// Takes back everything <see cref="TryApply"/> changed on the patient.
    /// </summary>
    private void Release(Entity<BedInternalsComponent> bed, EntityUid patient)
    {
        if (!TryComp<BedInternalsPatientComponent>(patient, out var state))
            return;

        // The patient may have switched to another tank in the meantime, then it is not ours to disconnect.
        if (TryComp<GasTankComponent>(state.BedTank, out var bedTank) && bedTank.User == patient)
            _gasTank.DisconnectFromInternals((state.BedTank.Value, bedTank), forced: true);

        if (state.BedMask is { } mask && !TerminatingOrDeleted(mask))
        {
            // A bed that is being deleted cannot take the mask back, so the patient keeps it as an ordinary mask.
            if (TerminatingOrDeleted(bed))
                RemComp<UnremoveableComponent>(mask);
            else
                ReturnMask(bed, patient, mask);
        }

        // Only when the patient is not already breathing from something else, which was their own choice.
        if (TryComp<InternalsComponent>(patient, out var internals)
            && internals.GasTankEntity == null
            && TryComp<GasTankComponent>(state.PreviousTank, out var previousTank)
            && !TerminatingOrDeleted(state.PreviousTank.Value))
        {
            _gasTank.ConnectToInternals((state.PreviousTank.Value, previousTank), patient);
        }

        RemComp<BedInternalsPatientComponent>(patient);
    }

    private void ReturnMask(Entity<BedInternalsComponent> bed, EntityUid patient, EntityUid mask)
    {
        if (!_inventory.TryGetSlotEntity(patient, MaskSlot, out var worn) || worn != mask)
            return;

        if (!_inventory.TryUnequip(patient, MaskSlot, silent: true, force: true))
            return;

        if (_container.TryGetContainer(bed, bed.Comp.MaskContainer, out var container))
            _container.Insert(mask, container);
    }

    private bool Fail(string messageId, EntityUid patient, EntityUid? user)
    {
        var message = Loc.GetString(messageId);
        if (user != null)
            _popup.PopupClient(message, patient, user, PopupType.SmallCaution);
        else if (_net.IsServer)
            _popup.PopupEntity(message, patient, PopupType.SmallCaution);

        return false;
    }

    private Entity<GasTankComponent>? GetTank(Entity<BedInternalsComponent> bed)
    {
        if (_itemSlots.GetItemOrNull(bed.Owner, bed.Comp.GasSlot) is not { } tank
            || !TryComp<GasTankComponent>(tank, out var tankComp))
            return null;

        return (tank, tankComp);
    }

    private EntityUid? GetBedMask(Entity<BedInternalsComponent> bed)
    {
        if (!_container.TryGetContainer(bed, bed.Comp.MaskContainer, out var container))
            return null;

        return container.ContainedEntities.FirstOrNull();
    }

    private void UpdateTankVisual(Entity<BedInternalsComponent> bed)
    {
        var visual = BedTankVisual.None;
        if (_itemSlots.GetItemOrNull(bed.Owner, bed.Comp.GasSlot) is { } tank
            && TryComp<BedTankVisualComponent>(tank, out var tankVisual))
        {
            visual = tankVisual.Visual;
        }

        _appearance.SetData(bed, BedInternalsVisuals.TankVisual, visual);
    }
}
