// SPDX-FileCopyrightText: 2026 MaiaArai <158123176+YaraaraY@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Funkystation.FirelockBolt.Components;
using Content.Shared.DoAfter;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Interaction;
using Content.Shared.Prying.Components;
using Content.Shared.Verbs;
using Content.Shared.Wires;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._Funkystation.FirelockBolt.EntitySystems;

public abstract partial class SharedFirelockBoltControlSystem : EntitySystem
{
    [Dependency] protected SharedDoorSystem DoorSystem = null!;
    [Dependency] private SharedDoAfterSystem _doAfter = null!;
    [Dependency] private SharedAudioSystem _audio = null!;
    [Dependency] private SharedUserInterfaceSystem _ui = null!;
    [Dependency] private IGameTiming _timing = null!;
    [Dependency] protected EntityQuery<DoorBoltComponent> DoorBoltQuery = default!;
    [Dependency] protected EntityQuery<DoorComponent> DoorQuery = default!;
    [Dependency] private EntityQuery<FirelockComponent> _firelockQuery;
    [Dependency] private EntityQuery<WiresPanelComponent> _wiresPanelQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FirelockBoltControlComponent, DoorStateChangedEvent>(OnDoorStateChanged);
        SubscribeLocalEvent<FirelockBoltControlComponent, DoorBoltsChangedEvent>(OnBoltsChanged);
        SubscribeLocalEvent<FirelockBoltControlComponent, ActivateInWorldEvent>(OnActivate, before: new[] { typeof(SharedDoorSystem) });
        SubscribeLocalEvent<FirelockBoltControlComponent, InteractUsingEvent>(OnInteractUsing, before: new[] { typeof(SharedDoorSystem) });
        SubscribeLocalEvent<FirelockBoltControlComponent, FirelockOverrideToggleDoAfterEvent>(OnOverrideToggleDoAfter);
        SubscribeLocalEvent<FirelockBoltControlComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<FirelockBoltControlComponent, BoundUIOpenedEvent>(OnBuiOpened);
        SubscribeLocalEvent<FirelockBoltControlComponent, FirelockOverrideSetMessage>(OnOverrideSetMessage);
        // after DoorBolt so crowbar can still get through hazard bolts
        SubscribeLocalEvent<FirelockBoltControlComponent, BeforePryEvent>(OnBeforePry, after: new[] { typeof(SharedDoorSystem) });

        SubscribeLocalEvent<FirelockBoltControlComponent, PriedEvent>(OnPried, before: new[] { typeof(SharedDoorSystem) });
    }

    private void OnDoorStateChanged(Entity<FirelockBoltControlComponent> ent, ref DoorStateChangedEvent args)
    {
        ApplyBoltForDoorState(ent, args.State);
        PushState(ent);
    }

    private void OnBoltsChanged(Entity<FirelockBoltControlComponent> ent, ref DoorBoltsChangedEvent args)
    {
        PushState(ent);
    }

    private void OnActivate(Entity<FirelockBoltControlComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        // record interaction time to detect manual door closes
        ent.Comp.LastManualInteractionTime = _timing.CurTime;

        if (!_wiresPanelQuery.TryComp(ent.Owner, out var panel) || !panel.Open)
            return;

        // if the door is not bolted, don't intercept the click. let the door open normally
        var isBolted = DoorBoltQuery.TryComp(ent.Owner, out var bolt) && bolt.BoltsDown;
        if (!isBolted)
            return;

        args.Handled = true;
        _ui.OpenUi(ent.Owner, FirelockOverrideUiKey.Key, args.User);
    }

    private void OnInteractUsing(Entity<FirelockBoltControlComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        ent.Comp.LastManualInteractionTime = _timing.CurTime;
    }

    private void OnGetVerbs(Entity<FirelockBoltControlComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!_wiresPanelQuery.TryComp(ent.Owner, out var panel) || !panel.Open)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("firelock-bolt-control-verb-open-ui"),
            Act = () => _ui.OpenUi(ent.Owner, FirelockOverrideUiKey.Key, user),
            Icon = new SpriteSpecifier.Texture(new ("/Textures/Interface/VerbIcons/settings.svg.192dpi.png"))
        });
    }

    private void OnBuiOpened(Entity<FirelockBoltControlComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (args.UiKey is FirelockOverrideUiKey)
            PushState(ent);
    }

    protected void PushState(Entity<FirelockBoltControlComponent> ent)
    {
        var bolted = DoorBoltQuery.TryComp(ent.Owner, out var bolt) && bolt.BoltsDown;
        _ui.SetUiState(ent.Owner, FirelockOverrideUiKey.Key, new FirelockOverrideBuiState(ent.Comp.Override, bolted));
    }

    private void OnOverrideSetMessage(Entity<FirelockBoltControlComponent> ent, ref FirelockOverrideSetMessage args)
    {
        if (ent.Comp.Override == args.Override)
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager,
            args.Actor,
            ent.Comp.ToggleDelay,
            new FirelockOverrideToggleDoAfterEvent(args.Override),
            ent.Owner,
            target: ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnOverrideToggleDoAfter(Entity<FirelockBoltControlComponent> ent, ref FirelockOverrideToggleDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        SetOverride(ent, args.TargetOverride);
    }

    private void OnBeforePry(Entity<FirelockBoltControlComponent> ent, ref BeforePryEvent args)
    {
        if (!args.Cancelled || ent.Comp.Override)
            return;

        if (!DoorBoltQuery.TryComp(ent.Owner, out var bolt) || !bolt.BoltsDown)
            return;

        args.Cancelled = false;
        args.Message = null;
    }

    private void OnPried(Entity<FirelockBoltControlComponent> ent, ref PriedEvent args)
    {
        // drop bolts before StartOpening, even when unpowered
        if (DoorBoltQuery.TryComp(ent.Owner, out var bolt) && bolt.BoltsDown)
            DoorSystem.SetBoltsDown((ent.Owner, bolt), false, args.User, predicted: true, force: true);
    }

    private void ApplyBoltForDoorState(Entity<FirelockBoltControlComponent> ent, DoorState state)
    {
        if (ent.Comp.Override || !DoorBoltQuery.TryComp(ent.Owner, out var bolt))
            return;

        switch (state)
        {
            case DoorState.Closing:
                ent.Comp.IsManualClose = (_timing.CurTime - ent.Comp.LastManualInteractionTime) < TimeSpan.FromSeconds(1.5);
                break;

            case DoorState.Closed:
            case DoorState.Welded:
                UpdateHazardBolts(ent);
                break;

            case DoorState.Open:
                DoorSystem.SetBoltsDown((ent.Owner, bolt), false, predicted: true, force: true);
                ent.Comp.IsManualClose = false;
                break;
        }
    }

    public void UpdateHazardBolts(Entity<FirelockBoltControlComponent> ent, FirelockComponent? firelock = null, DoorComponent? door = null)
    {
        if (ent.Comp.Override)
            return;

        if (firelock == null && !_firelockQuery.TryComp(ent.Owner, out firelock))
            return;

        if (door == null && !DoorQuery.TryComp(ent.Owner, out door))
            return;

        if (!DoorBoltQuery.TryComp(ent.Owner, out var bolt))
            return;

        var shouldBolt = firelock.Powered
            && (door.State == DoorState.Closed || door.State == DoorState.Welded)
            && (ent.Comp.AlarmActive || firelock.IsLocked);

        if (bolt.BoltsDown == shouldBolt)
            return;

        DoorSystem.SetBoltsDown((ent.Owner, bolt), shouldBolt, predicted: true, force: !shouldBolt);
        PushState(ent);
    }

    /// <summary>
    /// While on, hazard wont rebolt this door
    /// </summary>
    public void SetOverride(Entity<FirelockBoltControlComponent> ent, bool value, bool playSound = true)
    {
        if (ent.Comp.Override == value)
            return;

        ent.Comp.Override = value;
        Dirty(ent, ent.Comp);

        if (playSound)
        {
            var sound = value ? ent.Comp.EnableSound : ent.Comp.DisableSound;
            _audio.PlayPvs(sound, ent.Owner);
        }

        if (value)
        {
            if (DoorBoltQuery.TryComp(ent.Owner, out var bolt))
                DoorSystem.SetBoltsDown((ent.Owner, bolt), false, force: true);
        }
        else
        {
            UpdateHazardBolts(ent);
        }

        PushState(ent);
    }
}
