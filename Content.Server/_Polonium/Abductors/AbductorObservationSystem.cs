using Content.Shared._Polonium.Abductors;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Abductors;

public sealed partial class AbductorObservationSystem : EntitySystem
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(0.5);

    [Dependency] private AbductorShipSystem _ship = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private StationAiVisionSystem _vision = default!;
    [Dependency] private TurfSystem _turf = default!;

    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AbductorObservationConsoleComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<AbductorObservationConsoleComponent, AbductorSendPadActionEvent>(OnSendPad);
        SubscribeLocalEvent<AbductorObservationConsoleComponent, AbductorSendSelfActionEvent>(OnSendSelf);
        SubscribeLocalEvent<AbductorObservationConsoleComponent, AbductorRetrieveActionEvent>(OnRetrieve);
        SubscribeLocalEvent<AbductorObservationConsoleComponent, AbductorSetReleaseActionEvent>(OnSetRelease);
        SubscribeLocalEvent<AbductorObservationConsoleComponent, AbductorExitObservationActionEvent>(OnExit);
        SubscribeLocalEvent<AbductorObserverComponent, ComponentShutdown>(OnObserverShutdown);
        SubscribeLocalEvent<AbductorObserverComponent, PlayerDetachedEvent>(OnPlayerDetached);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextCheck)
            return;

        _nextCheck = _timing.CurTime + CheckInterval;

        var query = EntityQueryEnumerator<AbductorObserverComponent>();
        while (query.MoveNext(out var uid, out var observer))
        {
            if (TerminatingOrDeleted(observer.Console)
                || !_blocker.CanInteract(uid, observer.Console)
                || !_interaction.InRangeUnobstructed(uid, observer.Console))
            {
                RemCompDeferred<AbductorObserverComponent>(uid);
            }
        }
    }

    private void OnActivate(Entity<AbductorObservationConsoleComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        args.Handled = true;
        var user = args.User;
        if (!_ship.TryGetShip(user, out var ship))
        {
            _popup.PopupEntity(Loc.GetString("abductor-unfamiliar"), ent, user);
            return;
        }

        if (HasComp<AbductorObserverComponent>(user))
            return;

        if (ent.Comp.User is { } current && Exists(current))
        {
            _popup.PopupEntity(Loc.GetString("abductor-observation-busy"), ent, user);
            return;
        }

        if (GetStartView(ship) is not { } view)
        {
            _popup.PopupEntity(Loc.GetString("abductor-observation-no-signal"), ent, user);
            return;
        }

        var eye = SpawnAtPosition(ent.Comp.EyePrototype, view);
        var observer = EnsureComp<AbductorObserverComponent>(user);
        observer.Eye = eye;
        observer.Console = ent;
        observer.StationGrid = ship.Comp.StationGrid;
        observer.ReleasePoint = ship.Comp.ReleasePoint;
        Dirty(user, observer);

        ent.Comp.User = user;
        _eye.SetDrawFov(user, false);
        _eye.SetTarget(user, eye);
        _mover.SetRelay(user, eye);
        GrantActions(ent, user);
    }

    private void GrantActions(Entity<AbductorObservationConsoleComponent> ent, EntityUid user)
    {
        if (ent.Comp.ActionEntities.Count == 0)
        {
            foreach (var proto in ent.Comp.Actions)
            {
                EntityUid? action = null;
                if (_actions.AddAction(user, ref action, proto, ent))
                    ent.Comp.ActionEntities.Add(action.Value);
            }

            return;
        }

        for (var i = 0; i < ent.Comp.ActionEntities.Count; i++)
        {
            EntityUid? action = ent.Comp.ActionEntities[i];
            _actions.AddAction(user, ref action, ent.Comp.Actions[i], ent);
        }
    }

    private EntityCoordinates? GetStartView(Entity<AbductorShipComponent> ship)
    {
        if (ship.Comp.LastView is { } last && _ship.IsOnStationMap(ship, last))
            return last;

        return ship.Comp.StationGrid is { } grid && TryComp<MapGridComponent>(grid, out var gridComp)
            ? new EntityCoordinates(grid, gridComp.LocalAABB.Center)
            : null;
    }

    private void OnObserverShutdown(Entity<AbductorObserverComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<AbductorObservationConsoleComponent>(ent.Comp.Console, out var console))
        {
            console.User = null;
            foreach (var action in console.ActionEntities)
            {
                if (Exists(action))
                    _actions.RemoveAction(ent.Owner, action);
            }
        }

        if (_ship.TryGetShip(ent, out var ship) && Exists(ent.Comp.Eye))
            _ship.SetLastView(ship, Transform(ent.Comp.Eye).Coordinates);

        if (!TerminatingOrDeleted(ent))
        {
            _eye.SetTarget(ent, null);
            _eye.SetDrawFov(ent, true);
        }

        QueueDel(ent.Comp.Eye);
    }

    private void OnPlayerDetached(Entity<AbductorObserverComponent> ent, ref PlayerDetachedEvent args)
    {
        RemCompDeferred<AbductorObserverComponent>(ent);
    }

    private void OnSendPad(Entity<AbductorObservationConsoleComponent> ent, ref AbductorSendPadActionEvent args)
    {
        if (args.Handled || !TryGetLanding(args.Performer, out var ship, out var landing))
            return;

        if (!_ship.TryGetTelepad(ship, out var telepad))
        {
            _ship.Notify(args.Performer, Loc.GetString("abductor-no-telepad"));
            return;
        }

        args.Handled = true;
        _ship.BeginWarp(landing, landing, ent.Comp.DropDelay, telepad: telepad);
    }

    private void OnSendSelf(Entity<AbductorObservationConsoleComponent> ent, ref AbductorSendSelfActionEvent args)
    {
        if (args.Handled || !TryGetLanding(args.Performer, out _, out var landing))
            return;

        args.Handled = true;
        _ship.BeginWarp(landing, landing, ent.Comp.DropDelay, args.Performer);
    }

    private void OnRetrieve(Entity<AbductorObservationConsoleComponent> ent, ref AbductorRetrieveActionEvent args)
    {
        if (!args.Handled && _ship.TryGetShip(args.Performer, out var ship))
            args.Handled = _ship.TryRetrieve(ship, args.Performer, ent.Comp.RetrieveDelay);
    }

    private void OnSetRelease(Entity<AbductorObservationConsoleComponent> ent, ref AbductorSetReleaseActionEvent args)
    {
        if (args.Handled || !TryGetLanding(args.Performer, out var ship, out var landing))
            return;

        args.Handled = true;
        _ship.SetReleasePoint(ship, landing);

        var observer = Comp<AbductorObserverComponent>(args.Performer);
        observer.ReleasePoint = landing;
        Dirty(args.Performer, observer);
        _ship.Notify(args.Performer, Loc.GetString("abductor-release-set"));
    }

    private void OnExit(Entity<AbductorObservationConsoleComponent> ent, ref AbductorExitObservationActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        RemCompDeferred<AbductorObserverComponent>(args.Performer);
    }

    /// <summary>
    /// The tile under the observer's eye, if it is a free station floor that a camera can see.
    /// </summary>
    private bool TryGetLanding(EntityUid user, out Entity<AbductorShipComponent> ship, out EntityCoordinates landing)
    {
        landing = default;
        if (!_ship.TryGetShip(user, out ship) || !TryComp<AbductorObserverComponent>(user, out var observer))
            return false;

        var coords = Transform(observer.Eye).Coordinates;
        if (_transform.GetGrid(coords) is not { } gridUid
            || gridUid == ship.Owner
            || !TryComp<MapGridComponent>(gridUid, out var grid)
            || !TryComp<BroadphaseComponent>(gridUid, out var broadphase)
            || !_vision.IsAccessible((gridUid, broadphase, grid), _map.CoordinatesToTile(gridUid, grid, coords))
            || !_turf.TryGetTileRef(coords, out var tile)
            || !_ship.IsFreeFloor(coords))
        {
            _ship.Notify(user, Loc.GetString("abductor-landing-blocked"));
            return false;
        }

        landing = _turf.GetTileCenter(tile.Value);
        return true;
    }
}
