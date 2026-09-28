using System.Numerics;
using Content.Server.Explosion.EntitySystems;
using Content.Server.GameTicking.Rules;
using Content.Shared.Explosion.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Random.Helpers;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Polonium.EvacScenarios;

public sealed partial class EvacMeteorShowerSystem : GameRuleSystem<EvacMeteorShowerComponent>
{
    [Dependency] private EvacScenarioSystem _scenario = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const int DirectionAttempts = 5;

    private List<Entity<MapGridComponent>> _grids = new();
    private readonly List<Vector2i> _tiles = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EvacMeteorComponent, StartCollideEvent>(OnMeteorCollide);
    }

    private void OnMeteorCollide(Entity<EvacMeteorComponent> ent, ref StartCollideEvent args)
    {
        if (!args.OtherFixture.Hard
            || TerminatingOrDeleted(ent)
            || args.OtherEntity != ent.Comp.Shuttle && Transform(args.OtherEntity).GridUid != ent.Comp.Shuttle
            || !TryComp<ExplosiveComponent>(ent, out var explosive))
        {
            return;
        }

        _explosion.TriggerExplosive(ent, explosive, totalIntensity: explosive.TotalIntensity * ent.Comp.IntensityMultiplier);
    }

    protected override void Started(EntityUid uid, EvacMeteorShowerComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        component.Phases.Sort((a, b) => a.Start.CompareTo(b.Start));
    }

    protected override void ActiveTick(EntityUid uid, EvacMeteorShowerComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (component.Phases.Count == 0
            || !TryComp<EvacScenarioComponent>(uid, out var scenario)
            || !_scenario.TryGetFlightTime((uid, scenario), out var flightTime)
            || flightTime >= component.StopTime)
        {
            return;
        }

        component.NextWave ??= Timing.CurTime - flightTime + component.Phases[0].Start;

        if (Timing.CurTime < component.NextWave)
            return;

        var phase = component.Phases[0];
        foreach (var candidate in component.Phases)
        {
            if (candidate.Start <= flightTime)
                phase = candidate;
        }

        component.NextWave = Timing.CurTime + phase.MinInterval + (phase.MaxInterval - phase.MinInterval) * RobustRandom.NextDouble();

        foreach (var shuttle in _scenario.GetShuttles(scenario))
        {
            if (!InHyperspace(shuttle))
                continue;

            var count = RobustRandom.Next(phase.MinPerWave, phase.MaxPerWave + 1);
            for (var i = 0; i < count; i++)
            {
                ThrowMeteor(component, shuttle, RobustRandom.Pick(phase.Meteors));
            }
        }
    }

    private bool InHyperspace(EntityUid shuttle)
    {
        return TryComp(shuttle, out TransformComponent? xform) && HasComp<FTLMapComponent>(xform.MapUid);
    }

    private void ThrowMeteor(EvacMeteorShowerComponent component, EntityUid shuttle, EntProtoId meteor)
    {
        if (!TryComp<MapGridComponent>(shuttle, out var grid)
            || !TryComp(shuttle, out TransformComponent? xform)
            || !TryPickTarget((shuttle, grid), out var localTarget))
        {
            return;
        }

        var matrix = _transform.GetWorldMatrix(xform);
        var target = Vector2.Transform(localTarget, matrix);
        var bounds = grid.LocalAABB;
        Span<Vector2> corners =
        [
            Vector2.Transform(bounds.BottomLeft, matrix),
            Vector2.Transform(bounds.BottomRight, matrix),
            Vector2.Transform(bounds.TopLeft, matrix),
            Vector2.Transform(bounds.TopRight, matrix),
        ];

        var shuttleVelocity = _physics.GetMapLinearVelocity(shuttle);
        var course = shuttleVelocity.LengthSquared() > 0.01f
            ? Vector2.Normalize(shuttleVelocity)
            : RobustRandom.NextAngle().ToVec();

        for (var attempt = 0; attempt < DirectionAttempts; attempt++)
        {
            var direction = new Angle(RobustRandom.NextFloat(-1f, 1f) * component.Spread.Theta).RotateVec(course);

            // How far the hull goes from the target in that direction.
            var hullReach = 0f;
            foreach (var corner in corners)
            {
                hullReach = MathF.Max(hullReach, Vector2.Dot(corner - target, direction));
            }

            var distance = hullReach + RobustRandom.NextFloat(component.MinDistance, component.MaxDistance);
            var spawnPos = target + direction * distance;

            // Don't hit the escape pods and other shuttles flying next to this one.
            var path = new Box2Rotated(
                Box2.CenteredAround((spawnPos + target) / 2f, new Vector2(distance + 2f, 2f)),
                direction.ToAngle(),
                (spawnPos + target) / 2f);

            _grids.Clear();
            _map.FindGridsIntersecting(xform.MapID, path, ref _grids);

            if (_grids.Exists(other => other.Owner != shuttle))
                continue;

            var uid = Spawn(meteor, new MapCoordinates(spawnPos, xform.MapID));
            _physics.SetLinearVelocity(uid, shuttleVelocity - direction * component.Speed);
            _physics.SetAngularVelocity(uid, RobustRandom.NextFloat(-2f, 2f));

            if (component.ExplodeOnImpact)
            {
                var evacMeteor = EnsureComp<EvacMeteorComponent>(uid);
                evacMeteor.Shuttle = shuttle;
                evacMeteor.IntensityMultiplier = component.ImpactIntensityMultiplier;
            }

            return;
        }
    }

    private bool TryPickTarget(Entity<MapGridComponent> grid, out Vector2 localTarget)
    {
        _tiles.Clear();
        foreach (var tile in _map.GetAllTiles(grid, grid.Comp))
        {
            _tiles.Add(tile.GridIndices);
        }

        if (_tiles.Count == 0)
        {
            localTarget = default;
            return false;
        }

        localTarget = _map.GridTileToLocal(grid, grid.Comp, RobustRandom.Pick(_tiles)).Position;
        return true;
    }
}
