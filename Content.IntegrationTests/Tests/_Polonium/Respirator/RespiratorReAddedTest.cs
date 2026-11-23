using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Robust.Server.Console;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Exceptions;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Polonium.Respirator;

[TestFixture]
[TestOf(typeof(RespiratorSystem))]
public sealed class RespiratorReAddedTest : GameTest
{
    private static readonly ResPath BreathingMap = new("Maps/Test/Breathing/3by3-20oxy-80nit.yml");

    [Test]
    public async Task ReAddedRespiratorKeepsBreathing()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var console = server.ResolveDependency<IServerConsoleHost>();
        var runtimeLog = server.ResolveDependency<IRuntimeLog>();
        var mapLoader = entMan.System<MapLoaderSystem>();
        var mapSys = entMan.System<SharedMapSystem>();
        var respiratorSys = entMan.System<RespiratorSystem>();

        EntityUid human = default;
        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out var mapId);
            Assert.That(mapLoader.TryLoadGrid(mapId, BreathingMap, out var grid));
            human = entMan.SpawnAtPosition("MobHuman", new EntityCoordinates(grid!.Value.Owner, new Vector2(0.5f, 0.5f)));
        });

        await server.WaitPost(() =>
        {
            var netEntity = entMan.GetNetEntity(human);
            console.ExecuteCommand($"rmcomp {netEntity} Respirator");
        });
        await server.WaitRunTicks(1);
        await server.WaitPost(() =>
        {
            var netEntity = entMan.GetNetEntity(human);
            console.ExecuteCommand($"addcomp {netEntity} Respirator");
        });
        await server.WaitRunTicks(1);

        Assert.That(entMan.HasComponent<RespiratorComponent>(human));

        var exceptionsBefore = runtimeLog.ExceptionCount;
        var cycleTicks = 3 * (int) cfg.GetCVar(CVars.NetTickrate);

        await server.WaitRunTicks(cycleTicks);
        Assert.That(runtimeLog.ExceptionCount, Is.EqualTo(exceptionsBefore), runtimeLog.Display());

        await server.WaitPost(() => respiratorSys.UpdateSaturation(human, -100f));
        await server.WaitRunTicks(cycleTicks);
        Assert.That(runtimeLog.ExceptionCount, Is.EqualTo(exceptionsBefore), runtimeLog.Display());
    }
}
