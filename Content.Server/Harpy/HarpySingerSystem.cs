using Content.Server.Instruments;
using Content.Server.Speech.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Bed.Sleep;
using Content.Shared.Damage.ForceSay;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Harpy;
using Content.Shared.Instruments;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.UserInterface;
using Content.Shared.Zombies;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Harpy;

/// <summary>
/// Interrupts a harpy's singing (the MIDI UI) whenever they lose the ability to speak,
/// and blocks the UI from opening in the first place if they already cannot.
/// </summary>
public sealed partial class HarpySingerSystem : EntitySystem
{
    [Dependency] private InstrumentSystem _instrument = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private InventorySystem _inventorySystem = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InstrumentComponent, MobStateChangedEvent>(OnMobStateChangedEvent);
        SubscribeLocalEvent<GotEquippedEvent>(OnEquip);
        SubscribeLocalEvent<EntityZombifiedEvent>(OnZombified);
        SubscribeLocalEvent<InstrumentComponent, KnockedDownEvent>(OnKnockedDown);
        SubscribeLocalEvent<InstrumentComponent, StunnedEvent>(OnStunned);
        SubscribeLocalEvent<InstrumentComponent, SleepStateChangedEvent>(OnSleep);
        SubscribeLocalEvent<InstrumentComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<HarpySingerComponent, BoundUIClosedEvent>(OnBoundUIClosed);
        SubscribeLocalEvent<HarpySingerComponent, BoundUIOpenedEvent>(OnBoundUIOpened);

        // This is intended to intercept the UI event and stop the MIDI UI from opening if the
        // singer is unable to sing. Thus it needs to run before the ActivatableUISystem.
        SubscribeLocalEvent<HarpySingerComponent, OpenUiActionEvent>(OnInstrumentOpen, before: [typeof(ActivatableUISystem)]);
    }

    private void OnEquip(GotEquippedEvent args)
    {
        // Check if an item that makes the singer mumble is equipped to their face
        // (not their pockets!). As of writing, this should just be the muzzle.
        if (HasComp<MumbleAccentComponent>(args.Equipment) && args.Slot == "mask")
            CloseMidiUi(args.EquipTarget);
    }

    private void OnMobStateChangedEvent(EntityUid uid, InstrumentComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState is MobState.Critical or MobState.Dead)
            CloseMidiUi(args.Target);
    }

    private void OnZombified(ref EntityZombifiedEvent args)
    {
        CloseMidiUi(args.Target);
    }

    private void OnKnockedDown(EntityUid uid, InstrumentComponent component, ref KnockedDownEvent args)
    {
        CloseMidiUi(uid);
    }

    private void OnStunned(EntityUid uid, InstrumentComponent component, ref StunnedEvent args)
    {
        CloseMidiUi(uid);
    }

    private void OnSleep(EntityUid uid, InstrumentComponent component, ref SleepStateChangedEvent args)
    {
        if (args.FellAsleep)
            CloseMidiUi(uid);
    }

    /// <summary>
    /// Almost a copy of Content.Server.Damage.ForceSay.DamageForceSaySystem.OnDamageChanged.
    /// Done so because DamageForceSaySystem doesn't output an event. It still reuses the values
    /// from DamageForceSayComponent, so any tweaks to that keep ForceSay consistent with
    /// singing interruptions.
    /// </summary>
    private void OnDamageChanged(EntityUid uid, InstrumentComponent instrumentComponent, DamageChangedEvent args)
    {
        if (!TryComp<DamageForceSayComponent>(uid, out var component) ||
            args.DamageDelta == null ||
            !args.DamageIncreased ||
            args.DamageDelta.GetTotal() < component.DamageThreshold ||
            component.ValidDamageGroups == null)
            return;

        var totalApplicableDamage = FixedPoint2.Zero;
        foreach (var (group, value) in args.DamageDelta.GetDamagePerGroup(_prototype))
        {
            if (!component.ValidDamageGroups.Contains(group))
                continue;

            totalApplicableDamage += value;
        }

        if (totalApplicableDamage >= component.DamageThreshold)
            CloseMidiUi(uid);
    }

    /// <summary>
    /// Closes the MIDI UI if it is open.
    /// </summary>
    private void CloseMidiUi(EntityUid uid)
    {
        if (!HasComp<ActiveInstrumentComponent>(uid) || !TryComp<ActorComponent>(uid, out var actor))
            return;

        if (actor.PlayerSession.AttachedEntity is not { } ent)
            return;

        _instrument.ToggleInstrumentUi(uid, ent);
    }

    /// <summary>
    /// Prevent the player from opening the MIDI UI under some circumstances.
    /// </summary>
    private void OnInstrumentOpen(EntityUid uid, HarpySingerComponent component, OpenUiActionEvent args)
    {
        // CanSpeak covers all reasons you can't talk, including being incapacitated
        // (crit/dead), asleep, or being mute for any reason.
        var canNotSpeak = !_blocker.CanSpeak(uid);
        var zombified = HasComp<ZombieComponent>(uid);
        var muzzled = _inventorySystem.TryGetSlotEntity(uid, "mask", out var maskUid) &&
                      HasComp<MumbleAccentComponent>(maskUid);

        // Set this event as handled when the singer should be incapable of singing in order
        // to stop the ActivatableUISystem event from opening the MIDI UI.
        args.Handled = canNotSpeak || muzzled || zombified;

        if (args.Handled)
            _popupSystem.PopupEntity(Loc.GetString("no-sing-while-no-speak"), uid, uid, PopupType.Medium);
    }

    private void OnBoundUIClosed(EntityUid uid, HarpySingerComponent component, BoundUIClosedEvent args)
    {
        if (args.UiKey is not InstrumentUiKey)
            return;

        TryComp(uid, out AppearanceComponent? appearance);
        _appearance.SetData(uid, HarpyVisualLayers.Singing, SingingVisualLayer.False, appearance);
    }

    private void OnBoundUIOpened(EntityUid uid, HarpySingerComponent component, BoundUIOpenedEvent args)
    {
        if (args.UiKey is not InstrumentUiKey)
            return;

        TryComp(uid, out AppearanceComponent? appearance);
        _appearance.SetData(uid, HarpyVisualLayers.Singing, SingingVisualLayer.True, appearance);
    }
}
