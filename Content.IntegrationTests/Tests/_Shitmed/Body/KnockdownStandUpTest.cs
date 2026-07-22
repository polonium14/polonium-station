using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Body;


[TestFixture]
public sealed class KnockdownStandUpTest : GameTest
{
    [TestCase("MobCorgi")]
    [TestCase("MobHuman")]
    public async Task KnockedDownMobStandsBackUp(string prototype)
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();

        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid mob = default;

        await server.WaitPost(() =>
        {
            mob = sEntMan.SpawnEntity(prototype, coords);
            var stun = sEntMan.System<SharedStunSystem>();
            stun.TryKnockdown(mob, TimeSpan.FromSeconds(0.5), force: true);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(sEntMan.System<StandingStateSystem>().IsDown(mob), Is.True,
                "Setup failure: the knockdown didn't actually put the mob down.");
        });

        // 0.5s knockdown + 2s stand-up DoAfter at 30 tps, with generous margin.
        await pair.RunTicksSync(150);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(sEntMan.HasComponent<KnockedDownComponent>(mob), Is.False,
                    $"{prototype} should have auto-recovered from a timed knockdown.");
                Assert.That(sEntMan.System<StandingStateSystem>().IsDown(mob), Is.False,
                    $"{prototype} should be standing again after the knockdown expired.");
            });
        });
    }
}
