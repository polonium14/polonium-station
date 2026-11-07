using Content.Shared.Actions;
using Content.Shared.Bed.Sleep;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.DoAfter;
using Content.Shared.Gravity;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Mobs;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Zombies;

namespace Content.Shared._EinsteinEngines.Flight;

public abstract partial class SharedFlightSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedVirtualItemSystem _virtualItem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FlightComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<FlightComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<FlightComponent, ToggleFlightEvent>(OnToggleFlight);
        SubscribeLocalEvent<FlightComponent, FlightDoAfterEvent>(OnFlightDoAfter);
        SubscribeLocalEvent<FlightComponent, IsWeightlessEvent>(OnIsWeightless);
        SubscribeLocalEvent<FlightComponent, RefreshWeightlessModifiersEvent>(OnRefreshWeightlessModifiers);
        SubscribeLocalEvent<FlightComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<FlightComponent, EntityZombifiedEvent>(OnZombified);
        SubscribeLocalEvent<FlightComponent, KnockedDownEvent>(OnKnockedDown);
        SubscribeLocalEvent<FlightComponent, StunnedEvent>(OnStunned);
        SubscribeLocalEvent<FlightComponent, DownedEvent>(OnDowned);
        SubscribeLocalEvent<FlightComponent, SleepStateChangedEvent>(OnSleepStateChanged);
        SubscribeLocalEvent<FlightComponent, CuffedStateChangeEvent>(OnCuffedStateChange);
    }

    private void OnMapInit(Entity<FlightComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.ToggleActionEntity, ent.Comp.ToggleAction, ent);
        Dirty(ent);
    }

    private void OnShutdown(Entity<FlightComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.ToggleActionEntity);

        if (!TerminatingOrDeleted(ent))
            SetFlying(ent.AsNullable(), false);
    }

    public void SetFlying(Entity<FlightComponent?> ent, bool flying)
    {
        if (!Resolve(ent, ref ent.Comp, false) || ent.Comp.On == flying)
            return;

        ent.Comp.On = flying;
        _actions.SetToggled(ent.Comp.ToggleActionEntity, flying);
        _movementSpeed.RefreshWeightlessModifiers(ent.Owner);
        _gravity.RefreshWeightless(ent.Owner);

        if (flying)
            _virtualItem.TrySpawnUnremoveableVirtualItemInHand(ent.Owner, ent.Owner, _hands.GetHandCount(ent.Owner), true, true);
        else
            _virtualItem.DeleteInHandsMatching(ent.Owner, ent.Owner);

        Dirty(ent);
    }

    private void OnToggleFlight(Entity<FlightComponent> ent, ref ToggleFlightEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (ent.Comp.On)
        {
            SetFlying(ent.AsNullable(), false);
            return;
        }

        if (!CanFly(ent))
            return;

        var doAfter = new DoAfterArgs(EntityManager, ent, ent.Comp.ActivationDelay, new FlightDoAfterEvent(), ent, ent)
        {
            BlockDuplicate = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnFlightDoAfter(Entity<FlightComponent> ent, ref FlightDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        SetFlying(ent.AsNullable(), true);
    }

    private bool CanFly(Entity<FlightComponent> ent)
    {
        if (TryComp<CuffableComponent>(ent, out var cuffable) && !cuffable.CanStillInteract)
        {
            _popup.PopupClient(Loc.GetString("flight-cannot-fly"), ent, ent, PopupType.Medium);
            return false;
        }

        if (HasComp<ZombieComponent>(ent))
        {
            _popup.PopupClient(Loc.GetString("flight-cannot-use-wings"), ent, ent, PopupType.Medium);
            return false;
        }

        if (_standing.IsDown(ent.Owner))
        {
            _popup.PopupClient(Loc.GetString("flight-cannot-fly"), ent, ent, PopupType.Medium);
            return false;
        }

        return true;
    }

    private void OnIsWeightless(Entity<FlightComponent> ent, ref IsWeightlessEvent args)
    {
        if (!ent.Comp.On)
            return;

        args.IsWeightless = true;
        args.Handled = true;
    }

    private void OnRefreshWeightlessModifiers(Entity<FlightComponent> ent, ref RefreshWeightlessModifiersEvent args)
    {
        if (!ent.Comp.On)
            return;

        args.WeightlessModifier = ent.Comp.SpeedModifier;
    }

    private void OnMobStateChanged(Entity<FlightComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
            SetFlying(ent.AsNullable(), false);
    }

    private void OnZombified(Entity<FlightComponent> ent, ref EntityZombifiedEvent args)
    {
        SetFlying(ent.AsNullable(), false);
    }

    private void OnKnockedDown(Entity<FlightComponent> ent, ref KnockedDownEvent args)
    {
        SetFlying(ent.AsNullable(), false);
    }

    private void OnStunned(Entity<FlightComponent> ent, ref StunnedEvent args)
    {
        SetFlying(ent.AsNullable(), false);
    }

    private void OnDowned(Entity<FlightComponent> ent, ref DownedEvent args)
    {
        SetFlying(ent.AsNullable(), false);
    }

    private void OnSleepStateChanged(Entity<FlightComponent> ent, ref SleepStateChangedEvent args)
    {
        if (args.FellAsleep)
            SetFlying(ent.AsNullable(), false);
    }

    private void OnCuffedStateChange(Entity<FlightComponent> ent, ref CuffedStateChangeEvent args)
    {
        if (TryComp<CuffableComponent>(ent, out var cuffable) && !cuffable.CanStillInteract)
            SetFlying(ent.AsNullable(), false);
    }
}
