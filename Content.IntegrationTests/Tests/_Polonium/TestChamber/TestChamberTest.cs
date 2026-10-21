using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._Polonium.TestChamber;
using Content.IntegrationTests.Tests.Interaction;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._Polonium.TestChamber;
using Content.Shared._Polonium.TestChamber;
using Content.Shared.CCVar;
using Content.Shared.Maps;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Polonium.TestChamber;

/// <summary>
/// Every kit has to drop onto the floor around the console, never onto it, and clean up has to remove all of it.
/// Kits for PRs come and go, so this runs over whatever kits exist at the moment.
/// </summary>
[TestOf(typeof(TestChamberSystem))]
public sealed class TestChamberTest : GameTest
{
    private const int Size = 6;

    [Test]
    public async Task EveryKitSpawnsAndResets()
    {
        var map = await Pair.CreateTestMap();
        var entMan = Server.ResolveDependency<IEntityManager>();
        var protoMan = Server.ResolveDependency<IPrototypeManager>();
        var tileDefs = Server.ResolveDependency<ITileDefinitionManager>();
        var mapSys = entMan.System<SharedMapSystem>();
        var chamber = entMan.System<TestChamberSystem>();

        EntityUid console = default;
        await Server.WaitPost(() =>
        {
            var floor = new Tile(tileDefs["FloorSteel"].TileId);
            for (var x = -Size; x <= Size; x++)
            {
                for (var y = -Size; y <= Size; y++)
                {
                    mapSys.SetTile(map.Grid, new Vector2i(x, y), floor);
                }
            }

            console = entMan.SpawnEntity(TestChamberSystem.ConsolePrototype, mapSys.GridTileToLocal(map.Grid, map.Grid, Vector2i.Zero));
        });

        var kits = protoMan.EnumeratePrototypes<TestChamberKitPrototype>().ToList();
        Assert.That(kits, Is.Not.Empty, "There is no test chamber kit at all, the permanent TestSubjects kit is missing.");

        foreach (var kit in kits)
        {
            List<EntityUid>? spawned = null;
            await Server.WaitPost(() =>
            {
                spawned = chamber.SpawnKit((console, entMan.GetComponent<TestChamberConsoleComponent>(console)), kit);
            });
            await RunTicksSync(5);

            await Server.WaitAssertion(() =>
            {
                Assert.That(spawned, Is.Not.Null, $"Kit {kit.ID} found no free tile.");
                Assert.That(spawned!.Count, Is.EqualTo(kit.Entities.Values.Sum(v => Math.Max(v, 1))), $"Kit {kit.ID} spawned the wrong amount.");
                var consoleTile = mapSys.TileIndicesFor(map.Grid, entMan.GetComponent<TransformComponent>(console).Coordinates);
                foreach (var uid in spawned.Where(u => entMan.EntityExists(u)))
                {
                    var xform = entMan.GetComponent<TransformComponent>(uid);
                    if (xform.ParentUid != map.Grid.Owner)
                        continue; // picked up or put into a container by its own setup
                    Assert.That(mapSys.TileIndicesFor(map.Grid, xform.Coordinates), Is.Not.EqualTo(consoleTile),
                        $"Kit {kit.ID} dropped {entMan.ToPrettyString(uid)} onto the console.");
                }

                chamber.Reset();
            });
            await RunTicksSync(5);

            await Server.WaitAssertion(() =>
            {
                var left = entMan.EntityQuery<TestChamberSpawnedComponent>().Count();
                Assert.That(left, Is.Zero, $"Cleaning up after kit {kit.ID} left {left} entities.");
                Assert.That(entMan.EntityExists(console), "Cleaning up deleted the console.");
            });
        }
    }
}

/// <summary>
/// With the development cvar on, the first player to spawn gets the console next to them, and only one.
/// </summary>
[TestOf(typeof(TestChamberSystem))]
public sealed class TestChamberConsoleSpawnTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = false,
        Connected = true,
        InLobby = true,
        Map = "Dev",
    };

    [Test]
    public async Task ConsoleSpawnsNextToPlayer()
    {
        await OverrideCVar(Side.Server, CCVars.TestChamberConsole, true);
        await Pair.WaitClientCommand("toggleready True");
        await Pair.WaitCommand("startround");
        await Pair.RunTicksSync(10);

        var entMan = Server.ResolveDependency<IEntityManager>();
        var xformSys = entMan.System<SharedTransformSystem>();
        await Server.WaitAssertion(() =>
        {
            var player = ServerSession?.AttachedEntity;
            Assert.That(player, Is.Not.Null, "The player did not spawn.");

            var consoles = entMan.EntityQuery<TestChamberConsoleComponent, TransformComponent>().ToList();
            Assert.That(consoles, Has.Count.EqualTo(1), "Expected exactly one test chamber console.");

            var playerXform = entMan.GetComponent<TransformComponent>(player!.Value);
            Assert.That(xformSys.InRange(consoles[0].Item2.Coordinates, playerXform.Coordinates, 5f),
                "The console spawned far from the player.");
        });
    }
}

/// <summary>
/// The whole path through the window: open the console, pick a kit, drop it, clean up.
/// </summary>
[TestOf(typeof(TestChamberSystem))]
public sealed class TestChamberConsoleUiTest : InteractionTest
{
    private const string Kit = "TestSubjects";

    [Test]
    public async Task DropAndCleanUpThroughWindow()
    {
        for (var x = -3; x <= 4; x++)
        {
            for (var y = -3; y <= 3; y++)
            {
                await SetTile(Plating, SEntMan.GetNetCoordinates(MapData.GridCoords.Offset(new Vector2(x + 0.5f, y + 0.5f))), MapData.Grid);
            }
        }

        await SpawnTarget(TestChamberSystem.ConsolePrototype);
        await Activate();
        Assert.That(IsUiOpen(TestChamberUiKey.Key), "The console window did not open.");

        var window = GetWindow<TestChamberWindow>();
        var kit = ProtoMan.Index<TestChamberKitPrototype>(Kit);
        var kitButton = GetControlFromChildren<Button>(b => b.Text == kit.Name, window);
        await ClickControl(kitButton);
        await ClickControl<TestChamberWindow>("SpawnButton");
        await RunTicks(10);

        var expected = kit.Entities.Values.Sum(v => Math.Max(v, 1));
        Assert.That(SEntMan.EntityQuery<TestChamberSpawnedComponent>().Count(), Is.EqualTo(expected), "The kit was not dropped.");

        // Confirm button: first click arms it, the second one after its cooldown confirms
        await ClickControl<TestChamberWindow>("ResetButton");
        await RunSeconds(1);
        await ClickControl<TestChamberWindow>("ResetButton");
        await RunTicks(10);

        Assert.That(SEntMan.EntityQuery<TestChamberSpawnedComponent>().Count(), Is.Zero, "Cleaning up left kit entities behind.");
    }
}
