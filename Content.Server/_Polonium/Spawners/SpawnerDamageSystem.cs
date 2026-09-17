using Content.Shared._Shitmed.Body;
using Content.Shared.Damage.Systems;

namespace Content.Server._Polonium.Spawners;

public sealed partial class SpawnerDamageSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpawnerDamageComponent, SpawnerSpawnedEvent>(OnSpawned);
    }

    private void OnSpawned(Entity<SpawnerDamageComponent> ent, ref SpawnerSpawnedEvent args)
    {
        if (ent.Comp.WholeBody)
        {
            EnsureComp<WholeBodyDamageComponent>(args.Spawned);
            EnsureComp<SkipDamageBridgeComponent>(args.Spawned);
        }

        // exact numbers are the point, species resistances would skew them
        _damageable.ChangeDamage(args.Spawned, ent.Comp.Damage, ignoreResistances: true);
    }

    public override void Update(float frameTime)
    {
        // the bridge marker is meant to be transient and some systems strip it when they are done,
        // so it gets put back every tick
        var query = EntityQueryEnumerator<WholeBodyDamageComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            EnsureComp<SkipDamageBridgeComponent>(uid);
        }
    }
}
