using System.Numerics;
using System.Threading.Tasks;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Shitmed.Body;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared.Body;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

[TestFixture]
[TestOf(typeof(TraumaSystem))]
public sealed class LegPenaltyTest : GameTest
{
    [Test]
    public async Task SpawningAHumanDoesNotForceItDown()
    {
        var pair = Pair;
        var server = pair.Server;
        var sEntMan = server.ResolveDependency<Robust.Shared.GameObjects.IEntityManager>();

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
            var standing = sEntMan.GetComponent<StandingStateComponent>(human);
            Assert.That(standing.Standing, Is.True,
                "a freshly-spawned human with healthy legs should not be forced onto the ground");

            var moveComp = sEntMan.GetComponent<MovementSpeedModifierComponent>(human);
            Assert.That(moveComp.WalkSpeedModifier, Is.EqualTo(1f).Within(0.01f));
        });
    }

    private static void BreakBone(IEntityManager sEntMan, TraumaSystem sTrauma, BodyComponent body, ProtoId<OrganCategoryPrototype> category)
    {
        Assert.That(LimbTargetMap.TryGetOrganByCategory(sEntMan, body, category, out var organ), Is.True);
        var woundable = sEntMan.GetComponent<WoundableComponent>(organ);
        Assert.That(woundable.Bone, Is.Not.Null);

        var bone = woundable.Bone!.ContainedEntities[0];
        sTrauma.SetBoneIntegrity(bone, 0);
    }

    [Test]
    public async Task BothLegsBrokenCrawlsOnHealthyArmsAndCollapsesTheMob()
    {
        var pair = Pair;
        var server = pair.Server;
        var sEntMan = server.ResolveDependency<Robust.Shared.GameObjects.IEntityManager>();
        var sTrauma = server.System<TraumaSystem>();
        var sMovement = server.System<MovementSpeedModifierSystem>();

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
            var body = sEntMan.GetComponent<BodyComponent>(human);

            BreakBone(sEntMan, sTrauma, body, "LegLeft");
            BreakBone(sEntMan, sTrauma, body, "LegRight");

            sMovement.RefreshMovementSpeedModifiers(human);

            var moveComp = sEntMan.GetComponent<MovementSpeedModifierComponent>(human);
            Assert.That(moveComp.WalkSpeedModifier, Is.EqualTo(0.125f).Within(0.01f),
                "both legs Broken but both arms healthy should crawl at the two-arm crawl speed, not stop dead");

            var standing = sEntMan.GetComponent<StandingStateComponent>(human);
            Assert.That(standing.Standing, Is.False,
                "losing both legs should knock the mob down");

            var standAttempt = new StandAttemptEvent();
            sEntMan.EventBus.RaiseLocalEvent(human, standAttempt);
            Assert.That(standAttempt.Cancelled, Is.True,
                "a legless mob should not be able to stand back up");
        });
    }

    [Test]
    public async Task AllFourLimbsBrokenLeavesTheMobUnableToMoveAtAll()
    {
        var pair = Pair;
        var server = pair.Server;
        var sEntMan = server.ResolveDependency<Robust.Shared.GameObjects.IEntityManager>();
        var sTrauma = server.System<TraumaSystem>();
        var sMovement = server.System<MovementSpeedModifierSystem>();

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
            var body = sEntMan.GetComponent<BodyComponent>(human);

            BreakBone(sEntMan, sTrauma, body, "LegLeft");
            BreakBone(sEntMan, sTrauma, body, "LegRight");
            BreakBone(sEntMan, sTrauma, body, "ArmLeft");
            BreakBone(sEntMan, sTrauma, body, "ArmRight");

            sMovement.RefreshMovementSpeedModifiers(human);

            var moveComp = sEntMan.GetComponent<MovementSpeedModifierComponent>(human);
            Assert.That(moveComp.WalkSpeedModifier, Is.EqualTo(0f).Within(0.01f),
                "no functional legs or arms should mean no movement whatsoever, not even a crawl");
        });
    }
}
