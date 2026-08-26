using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Utility;
using YamlDotNet.RepresentationModel;
using Content.Server.Administration.Systems;
using Content.Server.GameTicking;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Spawners.Components;
using Content.Server.Station.Components;
using Content.Shared.CCVar;
using Content.Shared.Maps;
using Content.Shared.Roles;
using Content.Shared.Station.Components;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
namespace Content.IntegrationTests.Tests
{
    [TestFixture]
    public sealed class PostMapInitTest : GameTest
    {
        public override PoolSettings PoolSettings => new PoolSettings()
        {
            Connected = true,
            Dirty = true,
        };

        private const bool SkipTestMaps = true;
        private const string TestMapsPath = "/Maps/Test/";

        private static readonly string[] NoSpawnMaps =
        {
            "CentComm",
            "Dart"
        };

        private static readonly string[] Grids =
        {
            "/Maps/centcomm.yml",
            "/Maps/_Polonium/centcomm.yml",
            AdminTestArenaSystem.ArenaMapPath
        };

        private static readonly string[] GameMaps = GameDataScrounger.PrototypesOfKind<GameMapPrototype>().Where(x => x != PoolManager.TestMap).ToArray();
        private static readonly ResPath[] AllMapFiles = GameDataScrounger.FilesInDirectoryInVfs("/Maps", "*.yml");
        private static readonly ResPath[] ShuttleMapFiles = GameDataScrounger.FilesInDirectoryInVfs("/Maps/Shuttles", "*.yml");

        /// <summary>
        /// Asserts that specific files have been saved as grids and not maps.
        /// </summary>
        [Test, TestCaseSource(nameof(Grids))]
        [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.GridFill), false)]
        public async Task GridsLoadableTest(string mapFile)
        {
            var pair = Pair;
            var server = pair.Server;

            var entManager = server.ResolveDependency<IEntityManager>();
            var mapLoader = entManager.System<MapLoaderSystem>();
            var mapSystem = entManager.System<SharedMapSystem>();
            var cfg = server.ResolveDependency<IConfigurationManager>();
            var path = new ResPath(mapFile);

            await server.WaitPost(() =>
            {
                mapSystem.CreateMap(out var mapId);
                try
                {
                    Assert.That(mapLoader.TryLoadGrid(mapId, path, out var grid));
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to load map {mapFile}, was it saved as a map instead of a grid?", ex);
                }

                mapSystem.DeleteMap(mapId);
            });
        }

        /// <summary>
        /// Asserts that shuttles are loadable and have been saved as grids and not maps.
        /// </summary>
        [Test]
        [TestCaseSource(nameof(ShuttleMapFiles))]
        [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.GridFill), false)]
        public async Task ShuttlesLoadableTest(ResPath path)
        {
            var pair = Pair;
            var server = pair.Server;

            var entManager = server.ResolveDependency<IEntityManager>();
            var mapLoader = entManager.System<MapLoaderSystem>();
            var mapSystem = entManager.System<SharedMapSystem>();
            var cfg = server.ResolveDependency<IConfigurationManager>();

            await server.WaitPost(() =>
            {
                Assert.Multiple(() =>
                {
                    mapSystem.CreateMap(out var mapId);
                    try
                    {
                        Assert.That(mapLoader.TryLoadGrid(mapId, path, out _),
                            $"Failed to load shuttle {path}, was it saved as a map instead of a grid?");
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Failed to load shuttle {path}, was it saved as a map instead of a grid?",
                            ex);
                    }
                    mapSystem.DeleteMap(mapId);
                });
            });
        }

        [Test]
        [TestCaseSource(nameof(AllMapFiles))]
        public async Task NoSavedPostMapInitTest(ResPath map)
        {
            var pair = Pair;
            var server = pair.Server;

            var resourceManager = server.ResolveDependency<IResourceManager>();
            var loader = server.System<MapLoaderSystem>();

            var rootedPath = map.ToRootedPath();

            var isV7Map = false;

            // ReSharper disable once RedundantLogicalConditionalExpressionOperand
            if (SkipTestMaps && rootedPath.ToString().StartsWith(TestMapsPath, StringComparison.Ordinal))
            {
                return; // We just pass immediately.
            }

            if (!resourceManager.TryContentFileRead(rootedPath, out var fileStream))
            {
                Assert.Fail($"Map not found: {rootedPath}");
            }

            using var reader = new StreamReader(fileStream);
            var yamlStream = new YamlStream();

            yamlStream.Load(reader);

            var root = yamlStream.Documents[0].RootNode;
            var meta = root["meta"];
            var version = meta["format"].AsInt();

            if (version >= 7)
            {
                isV7Map = true;
            }
            else
            {
                var postMapInit = meta["postmapinit"].AsBool();
                Assert.That(postMapInit, Is.False, $"Map {map.Filename} was saved postmapinit");
            }

            var deps = server.ResolveDependency<IEntitySystemManager>().DependencyCollection;
            var ev = new BeforeEntityReadEvent();
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, ev);

            if (isV7Map)
            {
                Assert.That(IsPreInit(map, loader, deps, ev.RenamedPrototypes, ev.DeletedPrototypes));
            }

            // Check that the test actually does manage to catch post-init maps and isn't just blindly passing everything.
            // To that end, create a new post-init map and try verify it.
            var mapSys = server.System<SharedMapSystem>();
            MapId id = default;
            await server.WaitPost(() => mapSys.CreateMap(out id, runMapInit: false));
            await server.WaitPost(() => server.EntMan.Spawn(null, new MapCoordinates(0, 0, id)));

            // First check that a pre-init version passes
            var path = new ResPath($"{nameof(NoSavedPostMapInitTest)}.yml");
            Assert.That(loader.TrySaveMap(id, path));
            Assert.That(IsPreInit(path, loader, deps, ev.RenamedPrototypes, ev.DeletedPrototypes));

            // and the post-init version fails.
            await server.WaitPost(() => mapSys.InitializeMap(id));
            Assert.That(loader.TrySaveMap(id, path));
            Assert.That(IsPreInit(path, loader, deps, ev.RenamedPrototypes, ev.DeletedPrototypes), Is.False);
        }

        private bool IsPreInit(ResPath map,
            MapLoaderSystem loader,
            IDependencyCollection deps,
            Dictionary<string, string> renamedPrototypes,
            HashSet<string> deletedPrototypes)
        {
            if (!loader.TryReadFile(map, out var data))
            {
                Assert.Fail($"Failed to read {map}");
                return false;
            }

            var reader = new EntityDeserializer(deps,
                data,
                DeserializationOptions.Default,
                renamedPrototypes,
                deletedPrototypes);

            if (!reader.TryProcessData())
            {
                Assert.Fail($"Failed to process {map}");
                return false;
            }

            foreach (var mapId in reader.MapYamlIds)
            {
                var mapData = reader.YamlEntities[mapId];
                if (mapData.PostInit)
                    return false;
            }

            return true;
        }

        [Test, TestCaseSource(nameof(GameMaps))]
        [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.GridFill), false)]
        public async Task GameMapsLoadableTest(string mapProto)
        {
            var pair = Pair;
            var server = pair.Server;

            var entManager = server.ResolveDependency<IEntityManager>();
            var mapLoader = entManager.System<MapLoaderSystem>();
            var mapSystem = entManager.System<SharedMapSystem>();
            var protoManager = server.ResolveDependency<IPrototypeManager>();
            var ticker = entManager.EntitySysManager.GetEntitySystem<GameTicker>();
            var shuttleSystem = entManager.EntitySysManager.GetEntitySystem<ShuttleSystem>();
            var cfg = server.ResolveDependency<IConfigurationManager>();

            await server.WaitPost(() =>
            {
                MapId mapId;
                try
                {
                    var opts = DeserializationOptions.Default with { InitializeMaps = true };
                    ticker.LoadGameMap(protoManager.Index<GameMapPrototype>(mapProto), out mapId, opts);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to load map {mapProto}", ex);
                }

                mapSystem.CreateMap(out var shuttleMap);
                var largest = 0f;
                EntityUid? targetGrid = null;
                var memberQuery = entManager.GetEntityQuery<StationMemberComponent>();

                var grids = mapSystem.GetAllGrids(mapId).ToList();
                var gridUids = grids.Select(o => o.Owner).ToList();
                targetGrid = gridUids.First();

                foreach (var grid in grids)
                {
                    var gridEnt = grid.Owner;
                    if (!memberQuery.HasComponent(gridEnt))
                        continue;

                    var area = grid.Comp.LocalAABB.Width * grid.Comp.LocalAABB.Height;

                    if (area > largest)
                    {
                        largest = area;
                        targetGrid = gridEnt;
                    }
                }

                // Test shuttle can dock.
                // This is done inside gamemap test because loading the map takes ages and we already have it.
                var station = entManager.GetComponent<StationMemberComponent>(targetGrid!.Value).Station;
                if (entManager.TryGetComponent<StationEmergencyShuttleComponent>(station, out var stationEvac))
                {
                    var shuttlePath = stationEvac.EmergencyShuttlePath;
                    Assert.That(mapLoader.TryLoadGrid(shuttleMap, shuttlePath, out var shuttle),
                        $"Failed to load {shuttlePath}");

                    Assert.That(
                        shuttleSystem.TryFTLDock(shuttle!.Value.Owner,
                            entManager.GetComponent<ShuttleComponent>(shuttle!.Value.Owner),
                            targetGrid.Value),
                        $"Unable to dock {shuttlePath} to {mapProto}");
                }

                mapSystem.DeleteMap(shuttleMap);

                if (entManager.HasComponent<StationJobsComponent>(station))
                {
                    // Test that the map has valid latejoin spawn points or container spawn points
                    if (!NoSpawnMaps.Contains(mapProto))
                    {
                        var lateSpawns = 0;

                        lateSpawns += GetCountLateSpawn<SpawnPointComponent>(gridUids, entManager);
                        lateSpawns += GetCountLateSpawn<ContainerSpawnPointComponent>(gridUids, entManager);

                        Assert.That(lateSpawns, Is.GreaterThan(0), $"Found no latejoin spawn points on {mapProto}");
                    }

                    // Test all availableJobs have spawnPoints
                    // This is done inside gamemap test because loading the map takes ages and we already have it.
                    var comp = entManager.GetComponent<StationJobsComponent>(station);
                    var jobs = new HashSet<ProtoId<JobPrototype>>(comp.SetupAvailableJobs.Keys);

                    // Polonium, jak mamy spawny late join, to każda praca z brakującym spawnem zostanie tam zespawnowana.
                    var spawnPointsLateJoin = entManager.EntityQuery<SpawnPointComponent>()
                        .Where(x => x.SpawnType == SpawnPointType.LateJoin)
                        .Select(x => x.SpawnType);

                    if (!spawnPointsLateJoin.Any())
                    {
                        var spawnPoints = entManager.EntityQuery<SpawnPointComponent>()
                            .Where(x => x.SpawnType == SpawnPointType.Job && x.Job != null)
                            .Select(x => x.Job.Value);

                        jobs.ExceptWith(spawnPoints);

                        spawnPoints = entManager.EntityQuery<ContainerSpawnPointComponent>()
                            .Where(x => x.SpawnType is SpawnPointType.Job or SpawnPointType.Unset && x.Job != null)
                            .Select(x => x.Job.Value);

                        jobs.ExceptWith(spawnPoints);

                        Assert.That(jobs, Is.Empty, $"There is no spawnpoints for {string.Join(", ", jobs)} on {mapProto}.");
                    }
                }

                try
                {
                    mapSystem.DeleteMap(mapId);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to delete map {mapProto}", ex);
                }
            });
        }



        private static int GetCountLateSpawn<T>(List<EntityUid> gridUids, IEntityManager entManager)
            where T : ISpawnPoint, IComponent
        {
            var resultCount = 0;
            var queryPoint = entManager.AllEntityQueryEnumerator<T, TransformComponent>();
#nullable enable
            while (queryPoint.MoveNext(out T? comp, out var xform))
            {
                var spawner = (ISpawnPoint)comp;

                if (spawner.SpawnType is not SpawnPointType.LateJoin
                || xform.GridUid == null
                || !gridUids.Contains(xform.GridUid.Value))
                {
                    continue;
                }
#nullable disable
                resultCount++;
                break;
            }

            return resultCount;
        }

        [Test]
        [TestCaseSource(nameof(AllMapFiles))]
        [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.GridFill), false)]
        public async Task NonGameMapsLoadableTest(ResPath mapPath)
        {
            var pair = Pair;
            var server = pair.Server;

            var mapLoader = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<MapLoaderSystem>();
            var resourceManager = server.ResolveDependency<IResourceManager>();
            var protoManager = server.ResolveDependency<IPrototypeManager>();
            var cfg = server.ResolveDependency<IConfigurationManager>();

            var gameMaps = protoManager.EnumeratePrototypes<GameMapPrototype>().Select(o => o.MapPath).ToHashSet();


            if (gameMaps.Contains(mapPath))
            {
                // TODO: You might be able to save like, 1-2 seconds of test time if you eliminate these before
                //       actually needing a pair.
                return;
            }

            var rootedPath = mapPath.ToRootedPath();

            if (SkipTestMaps && rootedPath.ToString().StartsWith(TestMapsPath, StringComparison.Ordinal))
            {
                return;
            }

            await server.WaitPost(() =>
            {
                Assert.Multiple(() =>
                {
                    // This bunch of files contains a random mixture of both map and grid files.
                    // TODO MAPPING organize files
                    var opts = MapLoadOptions.Default with
                    {
                        DeserializationOptions = DeserializationOptions.Default with
                        {
                            InitializeMaps = true,
                            LogOrphanedGrids = false
                        }
                    };

                    HashSet<Entity<MapComponent>> maps;

                    try
                    {
                        Assert.That(mapLoader.TryLoadGeneric(mapPath, out maps, out _, opts));
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Failed to load map {mapPath}", ex);
                    }

                    try
                    {
                        foreach (var map in maps)
                        {
                            server.EntMan.DeleteEntity(map);
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Failed to delete map {mapPath}", ex);
                    }
                });
            });
        }
    }
}
