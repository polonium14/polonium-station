using Content.Server._Imperial.ErtCall;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.Server._Imperial.ErtCall;
public sealed class CallErtSystem : EntitySystem
{
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly MapLoaderSystem _map = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;

    /// <summary>
    /// Attempts to spawn an ERT shuttle using the specified call preset.
    /// </summary>
    /// <param name="preset">Preset configuration that defines the shuttle type and resource path to use for the ERT spawn attempt.</param>
    /// <returns>true if ERT shuttle was successfully spawned. Otherwise, false.</returns>
    public bool SpawnErt(ErtCallPresetPrototype preset)
    {
        var shuttleMapUid = _mapSystem.CreateMap();
        var mapId = Comp<MapComponent>(shuttleMapUid).MapId;

        var stations = _station.GetStations();
        
        if (stations.Count == 0)
            return false;

        var targetStation = stations[0];
        
        if (!TryComp(targetStation, out StationDataComponent? dataComp))
            return false;

        var targetGrid = _station.GetLargestGrid(dataComp);

        if (targetGrid == null)
            return false;

        if (_map.TryLoadGrid(mapId, new ResPath(preset.Path), out var entity))
        {
            if (!HasComp<ShuttleComponent>(entity))
                return false;

            if (!_shuttle.TryFTLProximity(entity.Value, targetGrid.Value))
                return false;

            _station.AddGridToStation(targetStation, entity.Value);
        }

        _mapSystem.DeleteMap(mapId);

        return true;
    }
}

