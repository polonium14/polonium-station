using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Systems;

namespace Content.Server.Mobs;

public sealed class FixtureRadiusOnDeathSystem : EntitySystem
{
    [Dependency] private SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FixtureRadiusOnDeathComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(Entity<FixtureRadiusOnDeathComponent> ent, ref MobStateChangedEvent args)
    {
        if (!TryComp<FixturesComponent>(ent, out var fixtures))
            return;

        if (args.NewMobState == MobState.Dead)
            Shrink((ent, ent.Comp, fixtures));
        else if (args.OldMobState == MobState.Dead)
            Restore((ent, ent.Comp, fixtures));
    }

    private void Shrink(Entity<FixtureRadiusOnDeathComponent, FixturesComponent> ent)
    {
        foreach (var (id, fixture) in ent.Comp2.Fixtures)
        {
            if (fixture.Shape is not PhysShapeCircle circle || circle.Radius <= ent.Comp1.Radius)
                continue;

            ent.Comp1.OriginialRadius.TryAdd(id, circle.Radius);
            _physics.SetPositionRadius(ent, id, fixture, circle, circle.Position, ent.Comp1.Radius, ent.Comp2);
        }
    }

    private void Restore(Entity<FixtureRadiusOnDeathComponent, FixturesComponent> ent)
    {
        foreach (var (id, radius) in ent.Comp1.OriginialRadius)
        {
            if (ent.Comp2.Fixtures.TryGetValue(id, out var fixture) && fixture.Shape is PhysShapeCircle circle)
                _physics.SetPositionRadius(ent, id, fixture, circle, circle.Position, radius, ent.Comp2);
        }

        ent.Comp1.OriginialRadius.Clear();
    }
}
