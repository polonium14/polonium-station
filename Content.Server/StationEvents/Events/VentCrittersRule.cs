using Content.Server.StationEvents.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Storage;
using Robust.Shared.Random;

namespace Content.Server.StationEvents.Events;

public sealed partial class VentCrittersRule : StationEventSystem<VentCrittersRuleComponent>
{
    /*
     * DO NOT COPY PASTE THIS TO MAKE YOUR MOB EVENT.
     * USE THE PROTOTYPE.
     */

    protected override void Started(EntityUid uid, VentCrittersRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!TryGetRandomStation(out var station))
        {
            return;
        }

        var validLocations = StationSystem.GetEntitiesWithComponentOnStation<VentCritterSpawnLocationComponent>(station.Value, true);
        if (validLocations.Count == 0)
            return;

        // guaranteed spawn
        if (component.SpecialEntries.Count > 0)
        {
            var specialEntry = RobustRandom.Pick(component.SpecialEntries);
            var specialSpawn = Transform(RobustRandom.Pick(validLocations)).Coordinates;
            Spawn(specialEntry.PrototypeId, specialSpawn);
        }

        foreach (var location in validLocations)
        {
            var spawns = EntitySpawnCollection.GetSpawns(component.Entries, RobustRandom);
            foreach (var spawn in spawns)
            {
                Spawn(spawn, Transform(location).Coordinates);
            }

            var specialSpawns = EntitySpawnCollection.GetSpawns(component.SpecialEntries, RobustRandom);
            foreach (var spawn in specialSpawns)
            {
                Spawn(spawn, Transform(location).Coordinates);
            }
        }
    }
}
