// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.IntegrationTests.Fixtures;
using Content.Server.Atmos.EntitySystems;
using NUnit.Framework;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Polonium.Atmos;

/// <summary>
/// A mob that is already burning when it first reaches a client used to spawn its fire light
/// during ComponentInit, before the mob was initialized, and trip an engine assert on the client.
/// </summary>
[TestFixture]
[TestOf(typeof(Content.Client.Atmos.EntitySystems.FireVisualizerSystem))]
public sealed class BurningMobStreamTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task BurningMobReachesAClientCleanly()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        await server.WaitPost(() =>
        {
            var burning = entMan.SpawnEntity("MobHuman", map.GridCoords);
            entMan.System<FlammableSystem>().SetFireStacks(burning, 5, ignite: true);
        });
        await pair.RunTicksSync(5);
        await server.WaitPost(() =>
        {
            var player = entMan.SpawnEntity("MobHuman", map.GridCoords);
            entMan.System<Content.Shared.Mind.SharedMindSystem>().ControlMob(ServerSession!.UserId, player);
        });
        await pair.RunTicksSync(30);
    }
}
