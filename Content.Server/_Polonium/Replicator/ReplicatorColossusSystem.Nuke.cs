using Content.Server.Nuke;
using Content.Shared._Polonium.Replicator;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;

namespace Content.Server._Polonium.Replicator;

public sealed partial class ReplicatorColossusSystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobThresholdSystem _mobThreshold = default!;

    private void InitializeNuke()
    {
        SubscribeLocalEvent<NukeExplodedEvent>(OnNukeExploded);
    }

    private void OnNukeExploded(NukeExplodedEvent args)
    {
        if (args.OwningStation is not { } grid)
            return;

        var station = _station.GetOwningStation(grid);
        var query = EntityQueryEnumerator<ReplicatorColossusComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid is not { } colossusGrid
                || colossusGrid != grid && (station == null || _station.GetOwningStation(colossusGrid) != station)
                || _mobState.IsDead(uid))
                continue;

            if (_mobThreshold.TryGetThresholdForState(uid, MobState.Dead, out var deadAt))
            {
                var damage = new DamageSpecifier();
                damage.DamageDict["Heat"] = deadAt.Value - _damageable.GetTotalDamage(uid) + 1;
                _damageable.ChangeDamage(uid, damage, ignoreResistances: true, ignoreGlobalModifiers: true);
            }

            if (!_mobState.IsDead(uid))
                _mobState.ChangeMobState(uid, MobState.Dead);
        }
    }
}
