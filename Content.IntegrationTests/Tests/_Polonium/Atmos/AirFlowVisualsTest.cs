using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Tests.Atmos;
using Content.Shared.Atmos;
using Content.Shared.CCVar;
using Content.Shared.Mind;
using Content.Shared.Tests;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using ClientAirFlow = Content.Client._Polonium.Atmos.AirFlow.AirFlowVisualsSystem;

namespace Content.IntegrationTests.Tests._Polonium.Atmos;

[TestFixture]
[TestOf(typeof(Content.Server._Polonium.Atmos.AirFlow.AirFlowVisualsSystem))]
public sealed class AirFlowVisualsTest : AtmosTest
{
    protected override ResPath? TestMapPath => new("Maps/Test/Atmospherics/tile_atmosphere_test_room.yml");

    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    /// <summary>
    /// The floor marker of the test room is three steps from the breach and the source marker five.
    /// </summary>
    private const int Radius = 4;

    /// <summary>
    /// Three seconds: time for the atmos cycle that notices the seal, and for the client to let the flow die down.
    /// Shorter than the time after which the client gives up on a flow by itself.
    /// </summary>
    private const int SealTicks = 90;

    /// <summary>
    /// A wall of a pressurized room is removed. The client must learn that air rushes towards the breach,
    /// only near it, and forget about it once the breach is sealed.
    /// </summary>
    [Test]
    public async Task BreachSendsFlowTowardsTheHole()
    {
        var markers = SEntMan.AllEntities<TestMarkerComponent>();
        Assert.That(GetMarker(markers, "wall", out var wallMarker));
        Assert.That(GetMarker(markers, "floor", out var floorMarker));
        Assert.That(GetMarker(markers, "source", out var farMarker));

        var xformSys = SEntMan.System<SharedTransformSystem>();
        var mapSys = SEntMan.System<SharedMapSystem>();
        var wallTile = Vector2i.Zero;
        var floorTile = Vector2i.Zero;
        var farTile = Vector2i.Zero;
        var wall = EntityUid.Invalid;

        await Server.WaitAssertion(() =>
        {
            var cfg = Server.ResolveDependency<IConfigurationManager>();
            cfg.SetCVar(CCVars.AirFlowVisualsEnabled, true);
            cfg.SetCVar(CCVars.AirFlowVisualsRadius, Radius);

            Assert.That(xformSys.TryGetGridTilePosition(wallMarker, out wallTile, MapData.Grid));
            Assert.That(xformSys.TryGetGridTilePosition(floorMarker, out floorTile, MapData.Grid));
            Assert.That(xformSys.TryGetGridTilePosition(farMarker, out farTile, MapData.Grid));

            foreach (var anchored in mapSys.GetAnchoredEntities(MapData.Grid.Owner, MapData.Grid.Comp, wallTile))
            {
                if (anchored != wallMarker)
                    wall = anchored;
            }

            Assert.That(wall.IsValid(), "The test room must have a wall on the tile of the wall marker.");

            foreach (var tile in RelevantAtmos.Comp.Tiles.Values)
            {
                if (tile.Air is { Immutable: false })
                    SetTilePressure(tile, Atmospherics.OneAtmosphere);
            }

            var player = SEntMan.SpawnAtPosition("MobObserver", new EntityCoordinates(MapData.Grid.Owner, floorTile + new Vector2(0.5f, 0.5f)));
            SEntMan.System<SharedMindSystem>().ControlMob(ServerSession!.UserId, player);
        });

        await Pair.RunTicksSync(30);

        var clientAirFlow = Client.System<ClientAirFlow>();
        var clientGrid = MapData.CGridUid;
        Assert.That(clientAirFlow.GetFlow(clientGrid, floorTile), Is.EqualTo(Vector2.Zero), "A sealed room must not report any flow.");

        await Server.WaitPost(() => SEntMan.DeleteEntity(wall));

        var strongest = 0f;
        var towardsBreach = false;
        var farFlow = 0f;
        var breachDirection = Vector2.Normalize((Vector2) wallTile - floorTile);

        for (var i = 0; i < 40; i++)
        {
            await Pair.RunTicksSync(5);
            var flow = clientAirFlow.GetFlow(clientGrid, floorTile);
            strongest = MathF.Max(strongest, flow.Length());
            farFlow = MathF.Max(farFlow, clientAirFlow.GetFlow(clientGrid, farTile).Length());
            towardsBreach |= flow.Length() > 0.05f && Vector2.Dot(Vector2.Normalize(flow), breachDirection) > 0.5f;
        }

        Assert.Multiple(() =>
        {
            Assert.That(strongest, Is.GreaterThan(0.05f), "The client never saw the air rushing out.");
            Assert.That(towardsBreach, "The flow on the floor must point at the breach.");
            Assert.That(farFlow, Is.Zero, "Tiles further from the breach than the radius must not be sent.");
        });

        Assert.That(clientAirFlow.GetFlow(clientGrid, floorTile).Length(), Is.GreaterThan(0.05f), "Sanity: the air must still be rushing when the breach gets sealed.");

        await Server.WaitPost(() =>
        {
            SEntMan.SpawnAtPosition("WallSolid", new EntityCoordinates(MapData.Grid.Owner, wallTile + new Vector2(0.5f, 0.5f)));
        });

        await Pair.RunTicksSync(SealTicks);
        Assert.That(clientAirFlow.GetFlow(clientGrid, floorTile).Length(), Is.LessThan(0.01f), "The flow must be gone right after the breach is sealed.");
    }
}
