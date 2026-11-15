using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Polonium.Body;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body;
using Content.Shared.Mobs.Systems;
using Content.Shared.Traits.Assorted;
using NUnit.Framework;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Polonium.Body;

/// <summary>
/// A humanoid with a dead or missing heart suffocates within seconds and cannot be defibrillated
/// until a working heart is in. Bodies without a character profile are left alone.
/// </summary>
[TestFixture]
public sealed class CirculationTest : GameTest
{
    private BodyDebugSystem Debug => SEntMan.System<BodyDebugSystem>();
    private CirculationSystem Circulation => SEntMan.System<CirculationSystem>();

    private EntityUid Organ(EntityUid body, string category)
    {
        return SEntMan.GetComponent<BodyComponent>(body).Organs!.ContainedEntities
            .Single(o => SEntMan.GetComponent<OrganComponent>(o).Category == category);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task NoWorkingHeartStarvesBodyOfOxygen(bool destroy)
    {
        var map = await Pair.CreateTestMap();
        var body = EntityUid.Invalid;
        var control = EntityUid.Invalid;
        var mobState = SEntMan.System<MobStateSystem>();
        var respirators = SEntMan.System<RespiratorSystem>();

        await Server.WaitAssertion(() =>
        {
            body = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            control = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            Assert.That(Circulation.IsStopped(body), Is.False, "A fresh body has a working heart.");

            var heart = Organ(body, "Heart");
            Assert.That(destroy ? Debug.TryKillOrgan(body, heart) : Debug.TryRemoveOrgan(body, heart));
            Assert.Multiple(() =>
            {
                Assert.That(Circulation.IsStopped(body));
                Assert.That(SEntMan.GetComponent<UnrevivableComponent>(body).ReasonMessage.Id, Is.EqualTo("defibrillator-no-heart"));
            });
        });

        await Pair.RunSeconds(20);

        await Server.WaitAssertion(() =>
        {
            var respirator = SEntMan.GetComponent<RespiratorComponent>(body);
            Assert.Multiple(() =>
            {
                Assert.That(respirator.Saturation, Is.EqualTo(respirator.MinSaturation), "All oxygen is gone after 20 seconds.");
                Assert.That(SEntMan.HasComponent<SleepingComponent>(body), "A body without oxygen passes out.");
                Assert.That(mobState.IsAlive(body), "It suffocates at the usual rate, so it is far from dead.");
                Assert.That(SEntMan.HasComponent<SleepingComponent>(control), Is.False, "Passing out is only for a stopped heart.");
            });

            // Oxygen given to the body does not stay.
            respirators.UpdateSaturation(body, respirator.MaxSaturation, respirator);
        });

        await Pair.RunSeconds(2);

        await Server.WaitAssertion(() =>
        {
            var respirator = SEntMan.GetComponent<RespiratorComponent>(body);
            Assert.That(respirator.Saturation, Is.LessThan(respirator.SuffocationThreshold));

            if (destroy)
                Assert.That(Debug.TryReviveOrgan(body, Organ(body, "Heart")));
            else
                Assert.That(Debug.TryInsertOrgan(body, "Heart"));

            Assert.Multiple(() =>
            {
                Assert.That(Circulation.IsStopped(body), Is.False);
                Assert.That(SEntMan.HasComponent<UnrevivableComponent>(body), Is.False, "A working heart makes the body revivable again.");
            });

            respirators.UpdateSaturation(body, respirator.MaxSaturation, respirator);
        });

        await Pair.RunSeconds(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<SleepingComponent>(body), Is.False, "With a heart and oxygen the body wakes up.");
        });
    }

    [Test]
    public async Task UnrevivableTraitSurvivesHeartSwap()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            SEntMan.AddComponent<UnrevivableComponent>(body);

            Assert.That(Debug.TryRemoveOrgan(body, Organ(body, "Heart")));
            Assert.That(Debug.TryInsertOrgan(body, "Heart"));
            Assert.That(SEntMan.HasComponent<UnrevivableComponent>(body), "The trait is not ours to remove.");
        });
    }

    [Test]
    public async Task BodiesWithoutProfileAreLeftAlone()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var monkey = SEntMan.SpawnEntity("MobMonkey", map.GridCoords);
            var heart = Organ(monkey, "Heart");
            var cap = SEntMan.GetComponent<OrganIntegrityComponent>(heart).IntegrityCap;
            SEntMan.System<TraumaSystem>().TrySetOrganDamageModifier(heart, cap, monkey, "CirculationTest");
            Assert.That(Circulation.IsStopped(monkey), Is.False);

            var diona = SEntMan.SpawnEntity("MobDiona", map.GridCoords);
            Assert.That(Circulation.IsStopped(diona), Is.False, "A diona never had a heart.");
        });
    }
}
