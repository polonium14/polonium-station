using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Polonium.Body;
using Content.Server._Polonium.Smoking;
using Content.Shared._Polonium.Smoking;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Polonium.Smoking;

[TestFixture]
[TestOf(typeof(SmokerSystem))]
public sealed class SmokerTest : GameTest
{
    private static readonly HashSet<ProtoId<OrganCategoryPrototype>> Lungs = ["Lungs"];

    private EntityUid _mob;

    private async Task SpawnSmoker()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitPost(() =>
        {
            _mob = SEntMan.Spawn("MobHuman", new MapCoordinates(Vector2.Zero, map.MapId));
            SEntMan.AddComponent<SmokerComponent>(_mob);
        });
        await Pair.RunTicksSync(5);
    }

    [Test]
    public async Task LungsStartScarred()
    {
        await SpawnSmoker();

        await Server.WaitAssertion(() =>
        {
            var lungs = SEntMan.System<OrganHealingSystem>().GetLivingOrgans(_mob, Lungs, []);
            Assert.That(lungs, Is.Not.Empty, "Sanity check: a human has lungs.");
            var healing = SEntMan.System<OrganHealingSystem>();
            foreach (var lung in lungs)
            {
                var share = (lung.Comp.OrganIntegrity / lung.Comp.IntegrityCap).Float();
                Assert.That(share, Is.InRange(0.6f, 0.9f), "A smoker's lung should keep 60-90% of its integrity.");

                healing.HealOrgan(lung, lung.Comp.IntegrityCap);
                Assert.That((lung.Comp.OrganIntegrity / lung.Comp.IntegrityCap).Float(), Is.EqualTo(share).Within(0.01f),
                    "Healing should not restore a smoker's scarred lung.");
            }
        });
    }

    [Test]
    public async Task WithdrawalEndsWithNicotine()
    {
        await SpawnSmoker();

        await Server.WaitPost(() =>
        {
            var smoker = SEntMan.GetComponent<SmokerComponent>(_mob);
            var now = Server.ResolveDependency<IGameTiming>().CurTime;
            smoker.LastNicotine = now - TimeSpan.FromMinutes(25);
        });
        await Pair.RunSeconds(2);

        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<SmokerComponent>(_mob).Stage, Is.EqualTo(2), "25 minutes without nicotine is stage two."));

        await Server.WaitPost(() =>
        {
            var solutions = SEntMan.System<SharedSolutionContainerSystem>();
            var bloodstream = SEntMan.GetComponent<BloodstreamComponent>(_mob);
            Assert.That(solutions.TryGetSolution(_mob, bloodstream.BloodSolutionName, out var blood, out _));
            Assert.That(solutions.TryAddReagent(blood!.Value, "Nicotine", FixedPoint2.New(1)));
        });
        await Pair.RunSeconds(2);

        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<SmokerComponent>(_mob).Stage, Is.Zero, "Nicotine should end the withdrawal."));
    }
}
