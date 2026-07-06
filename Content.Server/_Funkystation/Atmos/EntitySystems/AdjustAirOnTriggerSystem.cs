// SPDX-FileCopyrightText: 2025 Steve <marlumpy@gmail.com>
// SPDX-FileCopyrightText: 2025 marc-pelletier <113944176+marc-pelletier@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later AND MIT

using Content.Server.Atmos.EntitySystems;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._Funkystation.Atmos.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Trigger;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server._Funkystation.Atmos.EntitySystems;

[UsedImplicitly]
public sealed partial class AdjustAirOnTriggerSystem : XOnTriggerSystem<AdjustAirOnTriggerComponent>
{
    [Dependency] private AtmosphereSystem _atmosphereSystem = default!;
    [Dependency] private GasTileOverlaySystem _gasOverlaySystem = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private SharedTransformSystem _transformSystem = default!;

    protected override void OnTrigger(Entity<AdjustAirOnTriggerComponent> ent, EntityUid target, ref TriggerEvent args)
    {
        if (!_random.Prob(ent.Comp.Probability))
            return;

        var xform = Transform(target);

        var coords = xform.Coordinates;
        if (!coords.IsValid(EntityManager))
            return;

        var mapCoords = _transformSystem.ToMapCoordinates(coords);
        if (mapCoords.MapId == MapId.Nullspace)
            return;

        if (!_mapSystem.TryFindGridAt(mapCoords, out var gridUid, out var grid))
            return;

        var gridEntity = new Entity<GridAtmosphereComponent?, GasTileOverlayComponent?>(gridUid, CompOrNull<GridAtmosphereComponent>(gridUid), CompOrNull<GasTileOverlayComponent>(gridUid));

        Entity<MapAtmosphereComponent?>? mapEntity = null;
        if (xform.MapUid is { } mapUid)
        {
            mapEntity = new Entity<MapAtmosphereComponent?>(mapUid, CompOrNull<MapAtmosphereComponent>(mapUid));
        }

        var centerTile = _mapSystem.CoordinatesToTile(gridUid, grid, coords);

        var visited = new HashSet<Vector2i>();
        var queue = new Queue<(Vector2i Tile, float Distance)>();
        queue.Enqueue((centerTile, 0f));
        visited.Add(centerTile);

        while (queue.Count > 0)
        {
            var (currentTile, currentDistance) = queue.Dequeue();

            if (currentDistance > ent.Comp.Range)
                continue;

            if (!_mapSystem.TryGetTileRef(gridUid, grid, currentTile, out var tileRef))
                continue;

            var mixture = _atmosphereSystem.GetTileMixture(gridEntity, mapEntity, tileRef.GridIndices, excite: true);
            if (mixture == null)
                continue;

            foreach (var (gas, moles) in ent.Comp.GasAdjustments)
            {
                mixture.AdjustMoles(gas, moles);
            }

            if (ent.Comp.Temperature.HasValue)
            {
                mixture.Temperature = Math.Max(ent.Comp.Temperature.Value, Atmospherics.TCMB);
            }

            var directions = new[] { AtmosDirection.North, AtmosDirection.South, AtmosDirection.East, AtmosDirection.West };
            var offsets = new[] { new Vector2i(0, 1), new Vector2i(0, -1), new Vector2i(1, 0), new Vector2i(-1, 0) };

            for (var i = 0; i < directions.Length; i++)
            {
                var direction = directions[i];
                var offset = offsets[i];
                var neighborTile = currentTile + offset;

                if (visited.Contains(neighborTile))
                    continue;

                if (_atmosphereSystem.IsTileAirBlocked(gridUid, currentTile, direction, grid))
                    continue;

                var neighborDistance = MathF.Sqrt((neighborTile.X - centerTile.X) * (neighborTile.X - centerTile.X) +
                                                    (neighborTile.Y - centerTile.Y) * (neighborTile.Y - centerTile.Y));

                if (neighborDistance > ent.Comp.Range)
                    continue;

                queue.Enqueue((neighborTile, neighborDistance));
                visited.Add(neighborTile);
            }
        }

        _gasOverlaySystem.UpdateSessions();

        args.Handled = true;
    }
}
