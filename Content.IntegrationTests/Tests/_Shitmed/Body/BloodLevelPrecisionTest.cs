using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

[TestFixture]
public sealed class BloodLevelPrecisionTest : GameTest
{
    [Test]
    public async Task GetBloodLevelPreservesSubPercentPrecision()
    {
        var pair = Pair;
        var server = pair.Server;
        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sBloodstream = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SharedBloodstreamSystem>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid human = default;
        await server.WaitPost(() => { human = sEntMan.SpawnEntity("MobHuman", coords); });
        await pair.RunTicksSync(10);

        await server.WaitPost(() => { sBloodstream.TryBleedOut(human, FixedPoint2.New(7.77f)); });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var bloodLevel = sBloodstream.GetBloodLevel(human);
            var percent = bloodLevel * 100f;
            var wholePercent = MathF.Round(percent);

            Assert.That(MathF.Abs(percent - wholePercent), Is.GreaterThan(0.05f),
                $"Blood level percentage ({percent}) should retain sub-percent precision after " +
                "bleeding a non-round amount, not quantize to a whole percent like the old " +
                "FixedPoint2-ratio bug did.");
        });
    }
}
