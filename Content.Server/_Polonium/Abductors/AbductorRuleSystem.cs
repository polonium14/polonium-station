using Content.Server.GameTicking;
using Content.Server.Shuttles.Systems;
using Content.Shared._Polonium.Abductors;
using Content.Shared.Antag;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Rules;
using Content.Shared.Station.Components;
using Content.Shared.Station.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;

namespace Content.Server._Polonium.Abductors;

public sealed partial class AbductorRuleSystem : GameRuleSystem<AbductorRuleComponent>
{
    [Dependency] private AbductorCuratorSystem _curator = default!;
    [Dependency] private AbductorShipSystem _ship = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;
    [Dependency] private StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Has to run first: the rule's spawn points are looked up on the map stored from this event.
        SubscribeLocalEvent<AbductorRuleComponent, RuleLoadedGridsEvent>(OnLoadedGrids, before: new[] { typeof(ServerRuleGridsSystem) });
        SubscribeLocalEvent<AbductorRuleComponent, AfterAntagEntitySelectedEvent>(OnAntagSelected);
    }

    private void OnLoadedGrids(Entity<AbductorRuleComponent> ent, ref RuleLoadedGridsEvent args)
    {
        if (args.Grids.Count == 0)
            return;

        ent.Comp.Ship = args.Grids[0];
        var ship = EnsureComp<AbductorShipComponent>(args.Grids[0]);

        if (!_station.TryGetRandomStation<StationEventEligibleComponent>(out var station)
            || _station.GetLargestGrid(station.Value.Owner) is not { } target)
        {
            return;
        }

        var loadedMap = args.Map;
        foreach (var grid in args.Grids)
        {
            if (!_shuttle.TryFTLProximity(grid, target))
            {
                Log.Error($"{ToPrettyString(ent)} could not place {ToPrettyString(grid)} near {ToPrettyString(target)}.");
                return;
            }

            AlignWith(grid, target);
        }

        _ship.SetStationGrid((args.Grids[0], ship), target);
        args.Map = Transform(target).MapID;
        _map.DeleteMap(loadedMap);
    }

    private void AlignWith(EntityUid grid, EntityUid target)
    {
        var center = Comp<MapGridComponent>(grid).LocalAABB.Center;
        var (position, rotation) = _transform.GetWorldPositionRotation(grid);
        var targetRotation = _transform.GetWorldRotation(target);
        var worldCenter = position + rotation.RotateVec(center);
        _transform.SetWorldPositionRotation(grid, worldCenter - targetRotation.RotateVec(center), targetRotation);
    }

    private void OnAntagSelected(Entity<AbductorRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        if (!TryComp<AbductorComponent>(args.EntityUid, out var abductor))
            return;

        if (ent.Comp.Ship is { } shipUid && TryComp<AbductorShipComponent>(shipUid, out var ship))
            _ship.AddCrew((shipUid, ship), (args.EntityUid, abductor));

        if (args.Session != null)
            _curator.Say(abductor.ArrivalLine, Filter.SinglePlayer(args.Session));
    }
}
