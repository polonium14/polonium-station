using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._Polonium.Abductors;
using Content.Shared.Actions;
using Content.Shared.Administration.Logs;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Buckle.Components;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Maps;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Spawners.Components;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Abductors;

public sealed partial class AbductorShipSystem : EntitySystem
{
    private const float TelepadRange = 0.5f;

    private static readonly Vector2[] BesideTelepad = [new(0, -1), new(1, 0), new(-1, 0), new(0, 1)];
    private static readonly EntProtoId WarpEffect = "EffectAbductorWarp";
    private static readonly EntProtoId SparksEffect = "EffectSparks";
    private static readonly ProtoId<CurrencyPrototype> Credit = "AbductorCredit";
    private static readonly SoundSpecifier ChargeSound = new SoundPathSpecifier("/Audio/Effects/teleport_departure.ogg");
    private static readonly SoundSpecifier ArrivalSound = new SoundPathSpecifier("/Audio/Effects/teleport_arrival.ogg");

    private static readonly ProtoId<AbductorCuratorLinePrototype> FirstMarkLine = "AbductorCuratorFirstMark";
    private static readonly ProtoId<AbductorCuratorLinePrototype> FirstAboardLine = "AbductorCuratorFirstAboard";
    private static readonly ProtoId<AbductorCuratorLinePrototype> FirstExperimentLine = "AbductorCuratorFirstExperiment";
    private static readonly ProtoId<AbductorCuratorLinePrototype> CleanLine = "AbductorCuratorClean";
    private static readonly ProtoId<AbductorCuratorLinePrototype> SloppyLine = "AbductorCuratorSloppy";
    private static readonly ProtoId<AbductorCuratorLinePrototype> OrderDoneLine = "AbductorCuratorOrderDone";
    private static readonly ProtoId<AbductorCuratorLinePrototype> OrderMissedLine = "AbductorCuratorOrderMissed";

    [Dependency] private AbductorBatonSystem _baton = default!;
    [Dependency] private AbductorCuratorSystem _curator = default!;
    [Dependency] private AbductorOrderSystem _orders = default!;
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStoreSystem _store = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;

    private readonly HashSet<Entity<MobStateComponent>> _onTelepad = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AbductorComponent, AbductorSpecimenMarkedEvent>(OnMarked);
        SubscribeLocalEvent<AbductorComponent, AbductorRecallActionEvent>(OnRecall);
        SubscribeLocalEvent<AbductorComponent, AbductorRecallDoAfterEvent>(OnRecallDoAfter);
        SubscribeLocalEvent<AbductorComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<AbductorStatusConsoleComponent, ExaminedEvent>(OnConsoleExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<AbductorWarpComponent>();
        while (query.MoveNext(out var uid, out var warp))
        {
            if (_timing.CurTime < warp.ArriveAt)
                continue;

            FinishWarp(warp);
            QueueDel(uid);
        }
    }

    public void SetStationGrid(Entity<AbductorShipComponent> ship, EntityUid grid)
    {
        ship.Comp.StationGrid = grid;
    }

    public void SetLastView(Entity<AbductorShipComponent> ship, EntityCoordinates view)
    {
        ship.Comp.LastView = view;
    }

    public void SetReleasePoint(Entity<AbductorShipComponent> ship, EntityCoordinates point)
    {
        ship.Comp.ReleasePoint = point;
    }

    public void AddCrew(Entity<AbductorShipComponent> ship, Entity<AbductorComponent> abductor)
    {
        if (!ship.Comp.Crew.Contains(abductor))
            ship.Comp.Crew.Add(abductor);

        if (_mind.TryGetMind(abductor, out var mindId, out _))
        {
            ship.Comp.CrewMinds.Add(mindId);
            EnsureComp<AbductorResearcherComponent>(mindId).Experiments = ship.Comp.Experiments;
        }

        abductor.Comp.Ship = ship;
        Dirty(abductor);
        _actions.AddAction(abductor, ref abductor.Comp.RecallActionEntity, abductor.Comp.RecallAction);
        UpdateRecallAction(abductor, Transform(abductor));
    }

    public bool TryGetShip(EntityUid abductor, out Entity<AbductorShipComponent> ship)
    {
        ship = default;
        if (!TryComp<AbductorComponent>(abductor, out var comp)
            || comp.Ship is not { } uid
            || !TryComp<AbductorShipComponent>(uid, out var shipComp))
        {
            return false;
        }

        ship = (uid, shipComp);
        return true;
    }

    public bool TryGetTelepad(EntityUid ship, out EntityUid telepad)
    {
        var query = EntityQueryEnumerator<AbductorTelepadComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid != ship)
                continue;

            telepad = uid;
            return true;
        }

        telepad = default;
        return false;
    }

    /// <summary>
    /// Opens the Curator's window for everyone in the ship's crew.
    /// </summary>
    public void Say(Entity<AbductorShipComponent> ship, ProtoId<AbductorCuratorLinePrototype> line)
    {
        var listeners = Filter.Empty();
        foreach (var member in ship.Comp.Crew)
        {
            if (TryComp<ActorComponent>(member, out var actor))
                listeners.AddPlayer(actor.PlayerSession);
        }

        _curator.Say(line, listeners);
    }

    /// <summary>
    /// Shows a popup to this mob. World popups need a line of sight from the body,
    /// so someone at the observation console gets it at the cursor.
    /// </summary>
    public void Notify(EntityUid user, string message)
    {
        if (HasComp<AbductorObserverComponent>(user))
            _popup.PopupCursor(message, user);
        else
            _popup.PopupEntity(message, user, user);
    }

    /// <summary>
    /// Starts a teleport that the crew can see coming at <paramref name="marker"/>.
    /// </summary>
    public void BeginWarp(EntityCoordinates marker, EntityCoordinates destination, TimeSpan delay, EntityUid? subject = null, EntityUid? telepad = null, EntityUid? ship = null, EntityUid? retriever = null)
    {
        var uid = SpawnAtPosition(WarpEffect, marker);
        var warp = EnsureComp<AbductorWarpComponent>(uid);
        warp.Subject = subject;
        warp.Telepad = telepad;
        warp.Ship = ship;
        warp.Retriever = retriever;
        warp.Destination = destination;
        warp.ArriveAt = _timing.CurTime + delay;

        SpawnAtPosition(SparksEffect, marker);
        _audio.PlayPvs(ChargeSound, marker);
    }

    /// <summary>
    /// Pulls the marked specimen towards the telepad.
    /// </summary>
    public bool TryRetrieve(Entity<AbductorShipComponent> ship, EntityUid user, TimeSpan delay)
    {
        if (ship.Comp.Marked is not { } specimen || TerminatingOrDeleted(specimen))
        {
            Notify(user, Loc.GetString("abductor-retrieve-no-mark"));
            return false;
        }

        if (!TryGetTelepad(ship, out var telepad))
        {
            Notify(user, Loc.GetString("abductor-no-telepad"));
            return false;
        }

        if (_container.IsEntityInContainer(specimen) || IsBuckled(specimen) || !_baton.IsSubdued(specimen))
        {
            Notify(user, Loc.GetString("abductor-retrieve-blocked"));
            return false;
        }

        ship.Comp.Marked = null;
        _adminLog.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(user):user} pulled {ToPrettyString(specimen):target} aboard an abductor ship");
        BeginWarp(Transform(specimen).Coordinates, Transform(telepad).Coordinates, delay, specimen, ship: ship, retriever: user);
        return true;
    }

    public bool IsCatalogued(Entity<AbductorShipComponent> ship, EntityUid subject)
    {
        return ship.Comp.Catalogued.Contains(subject);
    }

    /// <summary>
    /// Enters a finished specimen into the register, pays the ship and has the Curator comment on the results.
    /// </summary>
    public void RecordExperiment(
        Entity<AbductorShipComponent> ship,
        EntityUid subject,
        bool clean,
        int credits,
        List<ProtoId<AbductorCuratorLinePrototype>> glandReports,
        bool? orderDone)
    {
        ship.Comp.Catalogued.Add(subject);
        ship.Comp.Experiments++;
        foreach (var mind in ship.Comp.CrewMinds)
        {
            if (TryComp<AbductorResearcherComponent>(mind, out var researcher))
                researcher.Experiments = ship.Comp.Experiments;
        }

        Pay(ship, credits);

        var first = ship.Comp.Experiments == 1;
        if (first)
            Say(ship, FirstExperimentLine);

        foreach (var report in glandReports)
        {
            Say(ship, report);
        }

        if (orderDone is { } done)
            Say(ship, done ? OrderDoneLine : OrderMissedLine);

        if (!first || !clean)
            Say(ship, clean ? CleanLine : SloppyLine);
    }

    public int GetExperiments(EntityUid mindId)
    {
        return CompOrNull<AbductorResearcherComponent>(mindId)?.Experiments ?? 0;
    }

    private void Pay(Entity<AbductorShipComponent> ship, int credits)
    {
        var query = EntityQueryEnumerator<AbductorStatusConsoleComponent, StoreComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var store, out var xform))
        {
            if (xform.GridUid != ship.Owner)
                continue;

            _store.TryAddCurrency(new Dictionary<ProtoId<CurrencyPrototype>, FixedPoint2> { [Credit] = credits }, uid, store);
            return;
        }
    }

    /// <summary>
    /// Where a finished specimen goes: the release point, the place it was taken from, or the arrivals.
    /// </summary>
    public EntityCoordinates? GetReturnPoint(Entity<AbductorShipComponent> ship, EntityUid subject)
    {
        if (ship.Comp.ReleasePoint is { } point && IsSafeReturn(ship, point))
            return point;

        if (TryComp<AbductorSpecimenComponent>(subject, out var specimen) && IsSafeReturn(ship, specimen.Origin))
            return specimen.Origin;

        var query = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        while (query.MoveNext(out var spawn, out var xform))
        {
            if (spawn.SpawnType == SpawnPointType.LateJoin && xform.GridUid == ship.Comp.StationGrid)
                return xform.Coordinates;
        }

        return null;
    }

    public bool IsOnStationMap(Entity<AbductorShipComponent> ship, EntityCoordinates coordinates)
    {
        return Exists(coordinates.EntityId)
            && (ship.Comp.StationGrid is not { } station
                || Exists(station) && _transform.GetMapId(coordinates) == Transform(station).MapID);
    }

    public bool IsFreeFloor(EntityCoordinates coordinates)
    {
        return _turf.TryGetTileRef(coordinates, out var tile)
            && !_turf.IsSpace(tile.Value)
            && !_turf.IsTileBlocked(tile.Value, CollisionGroup.MobMask);
    }

    public bool Teleport(EntityUid subject, EntityCoordinates destination, bool silent = false)
    {
        if (IsBuckled(subject))
            return false;

        StopPulling(subject);
        SpawnAtPosition(SparksEffect, Transform(subject).Coordinates);
        _transform.SetCoordinates(subject, destination);
        _transform.AttachToGridOrMap(subject);
        SpawnAtPosition(SparksEffect, destination);
        if (!silent)
            _audio.PlayPvs(ArrivalSound, destination);

        return true;
    }

    /// <summary>
    /// Breaks both the pull on this entity and the one it holds, so a teleport does not drag anyone along.
    /// </summary>
    public void StopPulling(EntityUid uid)
    {
        if (TryComp<PullableComponent>(uid, out var pullable) && pullable.BeingPulled)
            _pulling.TryStopPull(uid, pullable);

        if (TryComp<PullerComponent>(uid, out var puller)
            && puller.Pulling is { } pulled
            && TryComp<PullableComponent>(pulled, out var pulledComp))
        {
            _pulling.TryStopPull(pulled, pulledComp);
        }
    }

    private void FinishWarp(AbductorWarpComponent warp)
    {
        if (!Exists(warp.Destination.EntityId))
            return;

        if (warp.Telepad is { } telepad)
        {
            if (!Exists(telepad))
                return;

            _onTelepad.Clear();
            _lookup.GetEntitiesInRange(Transform(telepad).Coordinates, TelepadRange, _onTelepad, LookupFlags.Uncontained);
            foreach (var mob in _onTelepad)
            {
                Land(mob, warp.Destination);
            }

            return;
        }

        if (warp.Subject is not { } subject || TerminatingOrDeleted(subject))
            return;

        if (warp.Ship is { } shipUid && TryComp<AbductorShipComponent>(shipUid, out var ship))
            BringAboard((shipUid, ship), subject, warp.Destination, warp.Retriever);
        else
            Land(subject, warp.Destination);
    }

    private void Land(EntityUid mob, EntityCoordinates destination)
    {
        if (_container.IsEntityInContainer(mob))
            return;

        if (!IsFreeFloor(destination))
        {
            Notify(mob, Loc.GetString("abductor-landing-blocked"));
            return;
        }

        if (!Teleport(mob, destination))
            Notify(mob, Loc.GetString("abductor-teleport-buckled"));
    }

    private void BringAboard(Entity<AbductorShipComponent> ship, EntityUid specimen, EntityCoordinates destination, EntityUid? retriever)
    {
        var origin = Transform(specimen).Coordinates;
        if (!_container.IsEntityInContainer(specimen) && Teleport(specimen, destination))
        {
            OnSpecimenAboard(ship, specimen, origin);
            if (retriever is { } abductor)
                BringRetriever(abductor, destination);

            return;
        }

        ship.Comp.Marked ??= specimen;
        foreach (var member in ship.Comp.Crew)
        {
            if (!TerminatingOrDeleted(member))
                Notify(member, Loc.GetString("abductor-retrieve-blocked"));
        }
    }

    private void BringRetriever(EntityUid abductor, EntityCoordinates telepad)
    {
        if (TerminatingOrDeleted(abductor) || !HasComp<AbductorObserverComponent>(abductor))
            return;

        RemCompDeferred<AbductorObserverComponent>(abductor);
        if (!Teleport(abductor, GetSpotBeside(telepad), silent: true))
            Notify(abductor, Loc.GetString("abductor-teleport-buckled"));
    }

    private EntityCoordinates GetSpotBeside(EntityCoordinates center)
    {
        foreach (var offset in BesideTelepad)
        {
            var spot = center.Offset(offset);
            if (IsFreeFloor(spot))
                return spot;
        }

        return center;
    }

    private void OnParentChanged(Entity<AbductorComponent> ent, ref EntParentChangedMessage args)
    {
        if (!TerminatingOrDeleted(ent))
            UpdateRecallAction(ent, args.Transform);
    }

    private void UpdateRecallAction(Entity<AbductorComponent> ent, TransformComponent xform)
    {
        if (ent.Comp.Ship is not { } ship || _actions.GetAction(ent.Comp.RecallActionEntity, false) is not { } action)
            return;

        var aboard = xform.GridUid == ship;
        var shown = action.Comp.AttachedEntity == ent.Owner;
        if (aboard && shown)
            _actions.RemoveAction(ent.Owner, (action, action));
        else if (!aboard && !shown)
            _actions.AddAction(ent, ref ent.Comp.RecallActionEntity, ent.Comp.RecallAction);
    }

    private bool IsSafeReturn(Entity<AbductorShipComponent> ship, EntityCoordinates coordinates)
    {
        if (!IsOnStationMap(ship, coordinates) || !IsFreeFloor(coordinates) || !_turf.TryGetTileRef(coordinates, out var tile))
            return false;

        return !HasComp<GridAtmosphereComponent>(tile.Value.GridUid)
            || _atmosphere.GetTileMixture(tile.Value.GridUid, null, tile.Value.GridIndices) is { } air
            && air.Pressure >= Atmospherics.HazardLowPressure;
    }

    private void OnSpecimenAboard(Entity<AbductorShipComponent> ship, EntityUid specimen, EntityCoordinates origin)
    {
        EnsureComp<AbductorSpecimenComponent>(specimen).Origin = origin;

        if (!ship.Comp.HeardFirstAboard)
        {
            ship.Comp.HeardFirstAboard = true;
            Say(ship, FirstAboardLine);
        }

        _orders.Issue(ship, specimen);
    }

    private void OnMarked(Entity<AbductorComponent> ent, ref AbductorSpecimenMarkedEvent args)
    {
        if (!TryGetShip(ent, out var ship))
            return;

        ship.Comp.Marked = args.Specimen;
        _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(ent):user} marked {ToPrettyString(args.Specimen):target} for abduction");
        _popup.PopupEntity(Loc.GetString("abductor-baton-marked", ("target", Identity.Entity(args.Specimen, EntityManager))), args.Specimen, ent);

        if (ship.Comp.HeardFirstMark)
            return;

        ship.Comp.HeardFirstMark = true;
        Say(ship, FirstMarkLine);
    }

    private void OnRecall(Entity<AbductorComponent> ent, ref AbductorRecallActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryGetShip(ent, out var ship) || !TryGetTelepad(ship, out _))
        {
            Notify(ent, Loc.GetString("abductor-no-telepad"));
            return;
        }

        if (IsBuckled(ent))
        {
            Notify(ent, Loc.GetString("abductor-teleport-buckled"));
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, ent, ent.Comp.RecallDelay, new AbductorRecallDoAfterEvent(), ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        args.Handled = true;
        ent.Comp.RecallEffect = SpawnAttachedTo(WarpEffect, new EntityCoordinates(ent, Vector2.Zero));
        _audio.PlayPvs(ChargeSound, ent);
    }

    private void OnRecallDoAfter(Entity<AbductorComponent> ent, ref AbductorRecallDoAfterEvent args)
    {
        QueueDel(ent.Comp.RecallEffect);
        ent.Comp.RecallEffect = null;

        if (args.Cancelled || args.Handled || !TryGetShip(ent, out var ship) || !TryGetTelepad(ship, out var telepad))
            return;

        args.Handled = true;
        if (!Teleport(ent, Transform(telepad).Coordinates))
            Notify(ent, Loc.GetString("abductor-teleport-buckled"));
    }

    private void OnConsoleExamined(Entity<AbductorStatusConsoleComponent> ent, ref ExaminedEvent args)
    {
        if (!HasComp<AbductorComponent>(args.Examiner) || !TryComp<AbductorShipComponent>(Transform(ent).GridUid, out var ship))
            return;

        var credits = TryComp<StoreComponent>(ent, out var store)
            ? store.Balance.GetValueOrDefault(Credit)
            : FixedPoint2.Zero;

        args.PushMarkup(Loc.GetString("abductor-console-status",
            ("experiments", ship.Experiments),
            ("credits", credits.Int())));
    }

    private bool IsBuckled(EntityUid uid)
    {
        return TryComp<BuckleComponent>(uid, out var buckle) && buckle.Buckled;
    }
}
