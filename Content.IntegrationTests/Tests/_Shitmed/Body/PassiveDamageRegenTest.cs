using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Damage.Components;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

[TestFixture]
public sealed class PassiveDamageRegenTest : GameTest
{
    [Test]
    public async Task RealHumanHasNoPassiveDamageComponent()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid human = default;
        await server.WaitPost(() =>
        {
            human = sEntMan.SpawnEntity("MobHuman", coords);
        });

        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            Assert.That(sEntMan.HasComponent<PassiveDamageComponent>(human), Is.False,
                "A real human shouldn't have PassiveDamageComponent at all - it heals Blunt/Piercing/Slash unconditionally on its own 1s timer, completely bypassing WoundSystem's gate/bleed/trauma blocking, and Goob-Station has no equivalent on their base species mob.");
        });
    }
}
