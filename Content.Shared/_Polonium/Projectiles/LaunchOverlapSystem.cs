using System.Numerics;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._Polonium.Projectiles;

public sealed partial class LaunchOverlapSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
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

        // once out of the launch overlap it may be hit again, e.g. by a bola that bounced back
        if (LaunchBounds(ent, _physics.GetPhysicsTransform(ent), ent.Comp.FixtureId) is not { } bounds ||
            !bounds.Intersects(_lookup.GetWorldAABB(args.OtherEntity)))
        {
            ent.Comp.Ignored.Remove(args.OtherEntity);
            return;
        }

        args.Cancelled = IsIgnored(ent.AsNullable(), args.OtherEntity);
    }

    /// <summary>
    /// Whether a launched entity still passes through <paramref name="other"/>, which it overlapped at launch.
    /// </summary>
    public bool IsIgnored(Entity<LaunchOverlapComponent?> ent, EntityUid other)
    {
        if (!Resolve(ent, ref ent.Comp, false) ||
            !ent.Comp.Ignored.Contains(other) ||
            _timing.CurTime >= ent.Comp.EndTime)
        {
            return false;
        }

        // A reflected projectile belongs to the reflector now and may hit anyone.
        return ent.Comp.Shooter == null ||
               !TryComp(ent, out ProjectileComponent? projectile) ||
               projectile.Shooter == ent.Comp.Shooter;
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
        if (!xform.ParentUid.IsValid() || ent.Comp.LinearVelocity.LengthSquared() <= 0f)
            return;

        var direction = _transform.GetWorldRotation(xform.ParentUid).RotateVec(ent.Comp.LinearVelocity).Normalized();
        var (worldPos, worldRot) = _transform.GetWorldPositionRotation(xform);
        if (LaunchBounds(ent, new Transform(worldPos, worldRot), fixtureId) is not { } bounds)
            return;

        _overlapping.Clear();
        _lookup.GetEntitiesIntersecting(xform.MapID, bounds, _overlapping, LookupFlags.Dynamic | LookupFlags.Static);

        var target = CompOrNull<TargetedProjectileComponent>(ent)?.Target;
        foreach (var other in _overlapping)
        {
            if (other == ent.Owner || other == shooter || other == target)
                continue;

            // a locker or anything else the shooter sits in stays solid
            if (shooter != null && _transform.ContainsEntity(other, shooter.Value))
                continue;

            if (InLineOfFire(worldPos, direction, other))
                continue;

            comp ??= AddComp<LaunchOverlapComponent>(ent);
            comp.Ignored.Add(other);
        }

        if (comp == null)
            return;

        comp.Shooter = shooter;
        comp.FixtureId = fixtureId;
        comp.EndTime = _timing.CurTime + IgnoreTime;
    }

    private Box2? LaunchBounds(EntityUid uid, Transform transform, string? fixtureId)
    {
        if (!TryComp(uid, out FixturesComponent? fixtures))
            return null;

        Box2? bounds = null;
        foreach (var (id, fixture) in fixtures.Fixtures)
        {
            if (fixtureId != null && id != fixtureId)
                continue;

            for (var i = 0; i < fixture.Shape.ChildCount; i++)
            {
                var aabb = fixture.Shape.ComputeAABB(transform, i);
                bounds = bounds?.Union(aabb) ?? aabb;
            }
        }

        return bounds;
    }
}
