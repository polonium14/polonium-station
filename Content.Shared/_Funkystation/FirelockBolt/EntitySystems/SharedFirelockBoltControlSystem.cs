using Content.Shared._Funkystation.FirelockBolt.Components;
using Content.Shared.DoAfter;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Power.EntitySystems;
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
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] protected EntityQuery<DoorBoltComponent> DoorBoltQuery = default!;
    [Dependency] protected EntityQuery<DoorComponent> DoorQuery = default!;
    [Dependency] private EntityQuery<FirelockComponent> _firelockQuery;
    [Dependency] private EntityQuery<WiresPanelComponent> _wiresPanelQuery;
    [Dependency] private EntityQuery<PryUnpoweredComponent> _pryUnpoweredQuery;

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

        SubscribeLocalEvent<FirelockBoltControlComponent, BeforePryEvent>(OnBeforePry, after: new[] { typeof(SharedDoorSystem) });

        SubscribeLocalEvent<FirelockBoltControlComponent, PriedEvent>(OnPried, before: new[] { typeof(SharedDoorSystem) });

        SubscribeLocalEvent<FirelockBoltControlComponent, GetPryTimeModifierEvent>(
            OnGetPryTimeModifier,
            after: new[] { typeof(SharedDoorSystem), typeof(SharedFirelockSystem) });
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

        ent.Comp.LastManualInteractionTime = _timing.CurTime;

        if (!_wiresPanelQuery.TryComp(ent.Owner, out var panel) || !panel.Open)
            return;

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
        if (ent.Comp.Override)
            return;

        if (!DoorBoltQuery.TryComp(ent.Owner, out var bolt) || !bolt.BoltsDown)
            return;

        if (!_firelockQuery.TryComp(ent.Owner, out var firelock))
            return;

        // powered + bolted = lever only
        if (firelock.Powered)
        {
            args.Cancelled = true;
            return;
        }

        // unpowered + bolted = force-open tols
        args.Cancelled = false;
        args.Message = null;
    }

    private void OnGetPryTimeModifier(Entity<FirelockBoltControlComponent> ent, ref GetPryTimeModifierEvent args)
    {
        if (!_firelockQuery.TryComp(ent.Owner, out var firelock) || firelock.Powered)
            return;

        if (!DoorBoltQuery.TryComp(ent.Owner, out var bolt) || !bolt.BoltsDown)
            return;

        // if user is holding a prying tool, use its speed
        if (_hands.TryGetActiveItem(args.User, out var held)
            && TryComp<PryingComponent>(held, out var prying)
            && prying.Enabled)
        {
            args.BaseTime = TimeSpan.FromSeconds(3);
            args.PryTimeModifier = 1f;
            return;
        }

        // otherwise, use the default prying speed
        var handMod = _pryUnpoweredQuery.TryComp(ent.Owner, out var unpowered)
            ? unpowered.PryModifier
            : 0.5f;
        args.BaseTime = TimeSpan.FromSeconds(10 * handMod);
        args.PryTimeModifier = 1f;
    }

    private void OnPried(Entity<FirelockBoltControlComponent> ent, ref PriedEvent args)
    {
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
                ent.Comp.IsRemoteBolted = false;
                Dirty(ent, ent.Comp);
                ent.Comp.IsManualClose = false;
                break;
        }
    }

    public bool IsHazardous(Entity<FirelockBoltControlComponent> ent, FirelockComponent? firelock = null)
    {
        if (firelock == null && !_firelockQuery.TryComp(ent.Owner, out firelock))
            return ent.Comp.AlarmActive;

        return ent.Comp.AlarmActive || firelock.IsLocked;
    }

    /// <summary>
    /// Remote control is blocked while lever override is on
    /// </summary>
    public bool CanRemoteControl(Entity<FirelockBoltControlComponent> ent, out string? failReason)
    {
        if (ent.Comp.Override)
        {
            failReason = "firelock-bolt-control-remote-override";
            return false;
        }

        failReason = null;
        return true;
    }

    /// <summary>
    /// Wnen using remote or AI, try to set bolts down or up. Hazard blocks unbolt, override blocks everything
    /// </summary>
    public bool TrySetBoltsFromRemote(
        Entity<FirelockBoltControlComponent> ent,
        bool boltsDown,
        out string? failReason,
        EntityUid? user = null,
        bool predicted = true)
    {
        if (!CanRemoteControl(ent, out failReason))
            return false;

        if (!boltsDown && IsHazardous(ent))
        {
            failReason = "firelock-bolt-control-remote-hazard";
            return false;
        }

        if (!DoorBoltQuery.TryComp(ent.Owner, out var bolt))
        {
            failReason = null;
            return false;
        }

        if (bolt.BoltsDown != boltsDown)
        {
            if (!DoorSystem.TrySetBoltDown((ent.Owner, bolt), boltsDown, user, predicted))
            {
                failReason = null;
                return false;
            }
        }

        ent.Comp.IsRemoteBolted = boltsDown;
        Dirty(ent, ent.Comp);
        PushState(ent);
        failReason = null;
        return true;
    }

    /// <summary>
    /// hazard forces bolts on. safe drops them unless remote/AI is holding the bolt
    /// When no power, leave bolts alone so they stay pryable
    /// </summary>
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

        // use live power - FirelockComponent.Powered can lag behind PowerChanged order
        if (!firelock.Powered || !_power.IsPowered(ent.Owner))
            return;

        var closed = door.State == DoorState.Closed || door.State == DoorState.Welded;
        var hazardous = closed && IsHazardous(ent, firelock);

        if (hazardous)
        {
            if (!bolt.BoltsDown)
            {
                DoorSystem.SetBoltsDown((ent.Owner, bolt), true, predicted: true);
                PushState(ent);
            }

            return;
        }

        // safe - release unless remote wants them held
        if (!ent.Comp.IsRemoteBolted && bolt.BoltsDown)
        {
            DoorSystem.SetBoltsDown((ent.Owner, bolt), false, predicted: true);
            PushState(ent);
        }
    }

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
            ent.Comp.IsRemoteBolted = false;
            Dirty(ent, ent.Comp);

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
