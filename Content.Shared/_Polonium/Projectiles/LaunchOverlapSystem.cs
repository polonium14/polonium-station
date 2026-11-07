using System.Numerics;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Shared._Polonium.Projectiles;

public sealed partial class LaunchOverlapSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly float LineOfFireCos = MathF.Cos(MathF.PI / 4f);
    private static readonly TimeSpan IgnoreTime = TimeSpan.FromSeconds(1);
    private const float SamePositionDistance = 0.05f;

    private readonly HashSet<EntityUid> _overlapping = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PhysicsComponent, ProjectileShotEvent>(OnProjectileShot);
        SubscribeLocalEvent<ThrownEvent>(OnThrown);
        SubscribeLocalEvent<LaunchOverlapComponent, PreventCollideEvent>(OnPreventCollide);
    }

    private void OnProjectileShot(Entity<PhysicsComponent> ent, ref ProjectileShotEvent args)
    {
        if (!TryComp(ent, out ProjectileComponent? projectile))
            return;

        IgnoreOverlapping(ent, projectile.Shooter, SharedProjectileSystem.ProjectileFixture);
    }

    private void OnThrown(ref ThrownEvent args)
    {
        if (!TryComp(args.Thrown, out PhysicsComponent? physics))
            return;

        IgnoreOverlapping((args.Thrown, physics), args.User, null);
    }

    private void OnPreventCollide(Entity<LaunchOverlapComponent> ent, ref PreventCollideEvent args)
    {
        if (args.Cancelled || !ent.Comp.Ignored.Contains(args.OtherEntity))
            return;

        if (_timing.CurTime >= ent.Comp.EndTime)
        {
            RemCompDeferred<LaunchOverlapComponent>(ent);
            return;
        }

        // A reflected projectile belongs to the reflector now and may hit anyone.
        if (ent.Comp.Shooter != null &&
            TryComp(ent, out ProjectileComponent? projectile) &&
            projectile.Shooter != ent.Comp.Shooter)
        {
            return;
        }

        args.Cancelled = true;
    }

    /// <summary>
    /// Whether an entity is in front of a shot fired from <paramref name="origin"/> along the normalized
    /// <paramref name="direction"/>, rather than behind or beside the shooter.
    /// </summary>
    public bool InLineOfFire(Vector2 origin, Vector2 direction, EntityUid other)
    {
        var offset = _transform.GetWorldPosition(other) - origin;
        var distance = offset.Length();
        return distance > SamePositionDistance && Vector2.Dot(offset, direction) >= distance * LineOfFireCos;
    }

    private void IgnoreOverlapping(Entity<PhysicsComponent> ent, EntityUid? shooter, string? fixtureId)
    {
        if (TryComp(ent, out LaunchOverlapComponent? comp))
            comp.Ignored.Clear();

        var xform = Transform(ent);
        if (!xform.ParentUid.IsValid() ||
            ent.Comp.LinearVelocity.LengthSquared() <= 0f ||
            !TryComp(ent, out FixturesComponent? fixtures))
        {
            return;
        }

        var direction = _transform.GetWorldRotation(xform.ParentUid).RotateVec(ent.Comp.LinearVelocity).Normalized();
        var (worldPos, worldRot) = _transform.GetWorldPositionRotation(xform);
        var physicsTransform = new Transform(worldPos, worldRot);

        Box2? bounds = null;
        foreach (var (id, fixture) in fixtures.Fixtures)
        {
            if (fixtureId != null && id != fixtureId)
                continue;

            for (var i = 0; i < fixture.Shape.ChildCount; i++)
            {
                var aabb = fixture.Shape.ComputeAABB(physicsTransform, i);
                bounds = bounds?.Union(aabb) ?? aabb;
            }
        }

        if (bounds == null)
            return;

        _overlapping.Clear();
        _lookup.GetEntitiesIntersecting(xform.MapID, bounds.Value, _overlapping, LookupFlags.Dynamic | LookupFlags.Static);

        var target = CompOrNull<TargetedProjectileComponent>(ent)?.Target;
        foreach (var other in _overlapping)
        {
            if (other == ent.Owner || other == shooter || other == target)
                continue;

            if (InLineOfFire(worldPos, direction, other))
                continue;

            comp ??= AddComp<LaunchOverlapComponent>(ent);
            comp.Ignored.Add(other);
        }

        if (comp == null)
            return;

        comp.Shooter = shooter;
        comp.EndTime = _timing.CurTime + IgnoreTime;
    }
}
