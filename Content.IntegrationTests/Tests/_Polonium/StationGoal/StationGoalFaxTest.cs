// SPDX-FileCopyrightText: 2026 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 haze. <15122746+haze-14@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Utility;
using Content.Server._Corvax.StationGoal;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Shared.Maps;
using Content.Server.Station.Systems;
using Content.Shared.CCVar;
using Content.Shared.Fax.Components;
using Content.Shared.GameTicking;
using Robust.Shared.EntitySerialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Polonium.StationGoal;

[TestOf(typeof(StationGoalPaperSystem))]
public sealed class StationGoalFaxTest : GameTest
{
    private const string DefaultPool = "DefaultMapPool";

    private static readonly string[] GameMaps = GameDataScrounger.PrototypesOfKind<GameMapPrototype>()
        .Where(x => x != PoolManager.TestMap)
        .ToArray();

    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
    };

    [Test, TestCaseSource(nameof(GameMaps))]
    [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.GridFill), false)]
    public async Task StationGoalsReachStationFax(string mapProto)
    {
        if (!SProtoMan.Index<GameMapPoolPrototype>(DefaultPool).Maps.Contains(mapProto))
            Assert.Ignore($"{mapProto} is not in {DefaultPool}.");

        await Server.WaitPost(() =>
        {
            var ticker = SEntMan.System<GameTicker>();
            var stationSys = SEntMan.System<StationSystem>();
            var goalSys = SEntMan.System<StationGoalPaperSystem>();
            var mapSys = SEntMan.System<SharedMapSystem>();

            ticker.LoadGameMap(SProtoMan.Index<GameMapPrototype>(mapProto), out var mapId,
                DeserializationOptions.Default with { InitializeMaps = true });

            try
            {
                var stations = SEntMan.AllEntities<StationGoalComponent>().ToList();
                Assert.That(stations, Is.Not.Empty, $"{mapProto} has no station with a station goal.");

                var faxes = SEntMan.AllEntities<FaxMachineComponent>()
                    .Where(x => x.Comp.ReceiveStationGoal)
                    .ToList();

                foreach (var station in stations)
                {
                    var stationFaxes = faxes
                        .Where(x => stationSys.GetOwningStation(x.Owner) == station.Owner)
                        .ToList();

                    Assert.That(stationFaxes, Is.Not.Empty,
                        $"{mapProto}: no fax with receiveStationGoal belongs to the station, the goal would reach nobody.");
                }

                var queuedBefore = faxes.ToDictionary(x => x.Owner, x => x.Comp.PrintingQueue.Count);

                SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundStartedEvent(0));

                foreach (var fax in faxes)
                {
                    Assert.That(fax.Comp.PrintingQueue.Count - queuedBefore[fax.Owner], Is.EqualTo(1),
                        $"{mapProto}: fax {SEntMan.ToPrettyString(fax.Owner)} did not get exactly one goal paper.");
                }

                foreach (var station in stations)
                {
                    Assert.That(station.Comp.Goals, Is.Not.Empty, $"{mapProto} has an empty station goal list.");

                    foreach (var goal in station.Comp.Goals)
                    {
                        Assert.That(goalSys.SendStationGoal(station.Owner, goal), Is.True,
                            $"{mapProto}: goal {goal} was not received by any fax.");
                    }
                }
            }
            finally
            {
                mapSys.DeleteMap(mapId);
            }
        });
    }
}
