using Content.Server.StationEvents.Components;
using Content.Server.Storage.EntitySystems;
using Content.Shared.GameTicking.Components;
using Content.Shared.Storage.Components;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.StationEvents.Events;

public sealed partial class RandomEntityStorageSpawnRule : StationEventSystem<RandomEntityStorageSpawnRuleComponent>
{
    [Dependency] private EntityStorageSystem _entityStorage = default!;

    protected override void Started(EntityUid uid, RandomEntityStorageSpawnRuleComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);

        if (!TryGetRandomStation(out var station))
            return;

        var validLockers = new List<(EntityUid, EntityStorageComponent)>();
        var spawn = Spawn(comp.Prototype, MapCoordinates.Nullspace);

        foreach (var (ent, storage) in StationSystem.GetEntitiesWithComponentOnStation<EntityStorageComponent>(station.Value, false))
        {
            if (!_entityStorage.CanInsert(spawn, ent, storage))
                continue;

            validLockers.Add((ent, storage));
        }

        if (validLockers.Count == 0)
        {
            Del(spawn);
            return;
        }

        var (locker, storageComp) = RobustRandom.Pick(validLockers);
        if (!_entityStorage.Insert(spawn, locker, storageComp))
        {
            Del(spawn);
        }
        else if (comp.FirstOpenStorageSound != null)
        {
            storageComp.FirstOpenSound = comp.FirstOpenStorageSound;
            Dirty(locker, storageComp);
        }
    }
}
