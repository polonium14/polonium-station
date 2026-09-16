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
        // exact numbers are the point, species resistances would skew them
        _damageable.ChangeDamage(args.Spawned, ent.Comp.Damage, ignoreResistances: true);
    }
}
