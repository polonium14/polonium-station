using Content.Server.Administration.Logs;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.Database;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server._Polonium.Xenonids;

public sealed partial class XenoInfestationRule : StationEventSystem<XenoInfestationRuleComponent>
{
    [Dependency] private IAdminLogManager _adminLog = default!;

    protected override void Started(EntityUid uid, XenoInfestationRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!TryGetRandomStation(out var station))
        {
            EndSilently(uid, gameRule);
            return;
        }

        var locations = new List<EntityCoordinates>();
        foreach (var vent in StationSystem.GetEntitiesWithComponentOnStation<VentCritterSpawnLocationComponent>(station.Value, true))
        {
            locations.Add(Transform(vent).Coordinates);
        }

        if (locations.Count == 0)
        {
            Log.Warning($"No vents to spawn xeno larvae for {ToPrettyString(uid)}");
            EndSilently(uid, gameRule);
            return;
        }

        var coords = RobustRandom.Pick(locations);
        var count = RobustRandom.Next(component.MinLarvae, component.MaxLarvae + 1);
        for (var i = 0; i < count; i++)
        {
            Spawn(component.Larva, coords);
        }

        _adminLog.Add(LogType.EventStarted, LogImpact.High, $"Xeno infestation spawned {count} larvae at {coords}");
    }

    /// <summary>
    /// Bez larw nie ma czego ogłaszać, więc kończymy bez komunikatu końcowego.
    /// </summary>
    private void EndSilently(EntityUid uid, GameRuleComponent gameRule)
    {
        if (TryComp<StationEventComponent>(uid, out var stationEvent))
        {
            stationEvent.EndAnnouncement = null;
            stationEvent.EndAudio = null;
        }

        ForceEndSelf(uid, gameRule);
    }
}
