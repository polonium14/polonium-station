using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Xenonids.Announce;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Evolution;

public sealed partial class XenoEvolutionSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _action = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private EntityLookupSystem _entityLookup = default!;
    [Dependency] private SharedGameTicker _gameTicker = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedXenoAnnounceSystem _xenoAnnounce = default!;
    [Dependency] private SharedXenoHiveSystem _xenoHive = default!;

    private TimeSpan _evolutionAccumulatePointsBefore;
    private float _evolutionPointsRate;

    private readonly HashSet<EntityUid> _intersecting = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoEvolutionComponent, MapInitEvent>(OnXenoEvolveMapInit);
        SubscribeLocalEvent<XenoEvolutionComponent, XenoOpenEvolutionsActionEvent>(OnXenoEvolveAction);
        SubscribeLocalEvent<XenoEvolutionComponent, XenoEvolutionDoAfterEvent>(OnXenoEvolveDoAfter);
        SubscribeLocalEvent<XenoEvolutionComponent, NewXenoEvolvedEvent>(OnXenoEvolutionNewEvolved);

        SubscribeLocalEvent<XenoNewlyEvolvedComponent, PreventCollideEvent>(OnNewlyEvolvedPreventCollide);

        SubscribeLocalEvent<XenoEvolutionGranterComponent, NewXenoEvolvedEvent>(OnGranterEvolved);

        Subs.BuiEvents<XenoEvolutionComponent>(XenoEvolutionUIKey.Key, subs =>
        {
            subs.Event<XenoEvolveBuiMsg>(OnXenoEvolveBui);
        });

        Subs.CVar(_config, RMCCVars.RMCEvolutionPointsAccumulateBeforeMinutes,
            v => _evolutionAccumulatePointsBefore = TimeSpan.FromMinutes(v), true);
        Subs.CVar(_config, RMCCVars.RMCEvolutionPointsRate, v => _evolutionPointsRate = v, true);
    }

    private void OnXenoEvolveMapInit(Entity<XenoEvolutionComponent> ent, ref MapInitEvent args)
    {
        _action.AddAction(ent, ref ent.Comp.Action, ent.Comp.ActionId);
        Dirty(ent);
    }

    private void OnXenoEvolveAction(Entity<XenoEvolutionComponent> xeno, ref XenoOpenEvolutionsActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _ui.TryOpenUi(xeno.Owner, XenoEvolutionUIKey.Key, xeno);
        // push empty state so client BUI Refresh runs after open
        _ui.SetUiState(xeno.Owner, XenoEvolutionUIKey.Key, new XenoEvolveBuiState());
    }

    private void OnXenoEvolveBui(Entity<XenoEvolutionComponent> xeno, ref XenoEvolveBuiMsg args)
    {
        var actor = args.Actor;
        _ui.CloseUi(xeno.Owner, XenoEvolutionUIKey.Key, actor);

        if (_net.IsClient)
            return;

        if (!CanEvolvePopup(xeno, args.Choice))
        {
            Log.Warning($"{ToPrettyString(actor)} sent an invalid evolution choice: {args.Choice}.");
            return;
        }

        var ev = new XenoEvolutionDoAfterEvent(args.Choice);
        var doAfter = new DoAfterArgs(EntityManager, xeno, xeno.Comp.EvolutionDelay, ev, xeno)
        {
            BreakOnMove = false,
        };

        if (xeno.Comp.EvolutionDelay > TimeSpan.Zero)
            _popup.PopupClient(Loc.GetString("cm-xeno-evolution-start"), xeno, xeno);

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnXenoEvolveDoAfter(Entity<XenoEvolutionComponent> xeno, ref XenoEvolutionDoAfterEvent args)
    {
        if (_net.IsClient ||
            args.Handled ||
            args.Cancelled ||
            !_mind.TryGetMind(xeno, out _, out _) ||
            !CanEvolvePopup(xeno, args.Choice))
        {
            return;
        }

        args.Handled = true;

        var newXeno = TransferXeno(xeno, args.Choice);
        var evolved = new NewXenoEvolvedEvent(xeno);
        RaiseLocalEvent(newXeno, ref evolved, true);

        QueueDel(xeno.Owner);

        _popup.PopupEntity(Loc.GetString("cm-xeno-evolution-end"), newXeno, newXeno);

        var afterEv = new AfterNewXenoEvolvedEvent();
        RaiseLocalEvent(newXeno, ref afterEv);
    }

    private void OnXenoEvolutionNewEvolved(Entity<XenoEvolutionComponent> xeno, ref NewXenoEvolvedEvent args)
    {
        if (!TryComp(args.OldXeno, out XenoEvolutionComponent? old))
            return;

        xeno.Comp.Points = FixedPoint2.Max(0, old.Points - old.Max);
        Dirty(xeno);
    }

    private void OnNewlyEvolvedPreventCollide(Entity<XenoNewlyEvolvedComponent> ent, ref PreventCollideEvent args)
    {
        if (ent.Comp.StopCollide.Contains(args.OtherEntity))
            args.Cancelled = true;
    }

    private void OnGranterEvolved(Entity<XenoEvolutionGranterComponent> ent, ref NewXenoEvolvedEvent args)
    {
        _xenoAnnounce.AnnounceSameHive(ent.Owner, Loc.GetString("rmc-new-queen"));
    }

    public bool CanEvolvePopup(Entity<XenoEvolutionComponent> xeno, EntProtoId newXeno, bool doPopup = true)
    {
        var withoutPoints = xeno.Comp.EvolvesToWithoutPoints.Contains(newXeno);
        var withPoints = xeno.Comp.EvolvesTo.Contains(newXeno);

        if (!withoutPoints && !withPoints)
            return false;

        if (withPoints && xeno.Comp.Points < xeno.Comp.Max)
        {
            if (doPopup)
                _popup.PopupEntity(Loc.GetString("cm-xeno-evolution-failed-points"), xeno, xeno, PopupType.MediumCaution);
            return false;
        }

        // without-points path needs granter absent OR CanEvolveWithoutGranter
        if (withoutPoints && !withPoints)
        {
            if (!xeno.Comp.CanEvolveWithoutGranter && HasLivingGranter())
            {
                if (doPopup)
                    _popup.PopupEntity(Loc.GetString("cm-xeno-evolution-failed-hive-shaken"), xeno, xeno, PopupType.MediumCaution);
                return false;
            }
        }
        else if (xeno.Comp.RequiresGranter && !xeno.Comp.CanEvolveWithoutGranter && !HasLivingGranter())
        {
            if (doPopup)
                _popup.PopupEntity(Loc.GetString("cm-xeno-evolution-failed-hive-shaken"), xeno, xeno, PopupType.MediumCaution);
            return false;
        }

        if (!_prototypes.TryIndex(newXeno, out EntityPrototype? prototype))
            return true;

        if (prototype.TryGetComponent<XenoEvolutionGranterComponent>(out _, Factory) && HasLivingGranter())
        {
            if (doPopup)
                _popup.PopupEntity(Loc.GetString("rmc-xeno-evolution-failed-queen-exists"), xeno, xeno, PopupType.MediumCaution);
            return false;
        }

        return true;
    }

    public bool HasLivingGranter()
    {
        var query = EntityQueryEnumerator<XenoEvolutionGranterComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!_mobState.IsDead(uid))
                return true;
        }

        return false;
    }

    private EntityUid TransferXeno(EntityUid xeno, EntProtoId proto)
    {
        var coordinates = _transform.GetMoverCoordinates(xeno);
        var newXeno = Spawn(proto, coordinates);
        _xenoHive.SetSameHive(xeno, newXeno);

        if (_mind.TryGetMind(xeno, out var mindId, out var mind))
            _mind.TransferTo(mindId, newXeno, mind: mind);

        // dont get stuck in doors after evolving - client climb is flaky here
        var newly = EnsureComp<XenoNewlyEvolvedComponent>(newXeno);
        _intersecting.Clear();
        _entityLookup.GetEntitiesIntersecting(xeno, _intersecting);
        foreach (var id in _intersecting)
        {
            if (HasComp<DoorComponent>(id))
                newly.StopCollide.Add(id);
        }

        Dirty(newXeno, newly);
        return newXeno;
    }

    // for tests / admin tooling - sets points and runs the evolve path
    public bool TryForceEvolve(EntityUid xeno, EntProtoId choice)
    {
        if (!TryComp(xeno, out XenoEvolutionComponent? evolution))
            return false;

        if (!_mind.TryGetMind(xeno, out _, out _))
            return false;

        evolution.Points = evolution.Max;
        Dirty(xeno, evolution);

        if (!CanEvolvePopup((xeno, evolution), choice, doPopup: false))
            return false;

        var newXeno = TransferXeno(xeno, choice);
        var evolved = new NewXenoEvolvedEvent(xeno);
        RaiseLocalEvent(newXeno, ref evolved, true);
        QueueDel(xeno);

        var afterEv = new AfterNewXenoEvolvedEvent();
        RaiseLocalEvent(newXeno, ref afterEv);
        return true;
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var roundDuration = _gameTicker.RoundDuration();

        // stop accumulating after the cvar cutoff - but still clean newly evolved
        if (roundDuration <= _evolutionAccumulatePointsBefore)
        {
            var query = EntityQueryEnumerator<XenoEvolutionComponent>();
            while (query.MoveNext(out var uid, out var evolution))
            {
                if (evolution.Max <= FixedPoint2.Zero)
                    continue;

                if (evolution.Points >= evolution.Max)
                {
                    if (!evolution.GotPopup)
                    {
                        evolution.GotPopup = true;
                        Dirty(uid, evolution);
                        _popup.PopupEntity(Loc.GetString("cm-xeno-evolution-ready"), uid, uid, PopupType.Large);
                    }

                    continue;
                }

                if (evolution.LastPointsAt == TimeSpan.Zero)
                {
                    evolution.LastPointsAt = time;
                    Dirty(uid, evolution);
                    continue;
                }

                var elapsed = time - evolution.LastPointsAt;
                if (elapsed <= TimeSpan.Zero)
                    continue;

                evolution.LastPointsAt = time;
                evolution.Points = FixedPoint2.Min(evolution.Max,
                    evolution.Points + evolution.PointsPerSecond * _evolutionPointsRate * elapsed.TotalSeconds);
                Dirty(uid, evolution);
            }
        }

        var newly = EntityQueryEnumerator<XenoNewlyEvolvedComponent>();
        while (newly.MoveNext(out var uid, out var comp))
        {
            _intersecting.Clear();
            _entityLookup.GetEntitiesIntersecting(uid, _intersecting);

            for (var i = comp.StopCollide.Count - 1; i >= 0; i--)
            {
                if (!_intersecting.Contains(comp.StopCollide[i]))
                    comp.StopCollide.RemoveAt(i);
            }

            if (comp.StopCollide.Count == 0)
                RemCompDeferred<XenoNewlyEvolvedComponent>(uid);
            else
                Dirty(uid, comp);
        }
    }
}
