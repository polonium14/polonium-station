using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._EinsteinEngines.Flight;
using Content.Shared._EinsteinEngines.Flight;
using Content.Server.Disposal.Holder;
using Content.Shared.Actions;
using Content.Shared.Disposal.Unit;
using Content.Shared.Gravity;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Movement.Components;
using Content.Shared.StepTrigger.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._EinsteinEngines.Flight;

[TestFixture]
[TestOf(typeof(FlightSystem))]
public sealed class HarpyFlightTest : GameTest
{
    [Test]
    public async Task FlightMakesHarpyWeightlessFasterAndHandsBusy()
    {
        var server = Pair.Server;
        var entMan = server.EntMan;
        var flightSys = server.System<SharedFlightSystem>();
        var gravity = server.System<SharedGravitySystem>();
        var hands = server.System<SharedHandsSystem>();
        var testMap = await Pair.CreateTestMap();

        EntityUid harpy = default;

        await server.WaitAssertion(() =>
        {
            harpy = entMan.SpawnEntity("MobHarpy", testMap.GridCoords);
            var flight = entMan.GetComponent<FlightComponent>(harpy);
            var speed = entMan.GetComponent<MovementSpeedModifierComponent>(harpy);

            Assert.That(flight.ToggleActionEntity, Is.Not.Null);
            Assert.That(flight.On, Is.False);

            flightSys.SetFlying((harpy, flight), true);

            Assert.That(gravity.IsWeightless(harpy));
            Assert.That(speed.WeightlessModifier, Is.EqualTo(flight.SpeedModifier));

            var handCount = hands.GetHandCount(harpy);
            var virtualItems = 0;
            foreach (var held in hands.EnumerateHeld(harpy))
            {
                if (entMan.HasComponent<VirtualItemComponent>(held))
                    virtualItems++;
            }

            Assert.That(handCount, Is.GreaterThan(0));
            Assert.That(virtualItems, Is.EqualTo(handCount));

            flightSys.SetFlying((harpy, flight), false);

            Assert.That(speed.WeightlessModifier, Is.Not.EqualTo(flight.SpeedModifier));
        });

        await Pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(hands.EnumerateHeld(harpy), Is.Empty);
        });
    }

    [Test]
    public async Task HarpyIgnoresShardsAndLandMines()
    {
        var server = Pair.Server;
        var entMan = server.EntMan;
        var testMap = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var harpy = entMan.SpawnEntity("MobHarpy", testMap.GridCoords);
            var human = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            var shard = entMan.SpawnEntity("ShardGlass", testMap.GridCoords);
            var mine = entMan.SpawnEntity("LandMineExplosive", testMap.GridCoords);

            foreach (var trap in new[] { shard, mine })
            {
                var harpyEv = new StepTriggerAttemptEvent { Source = trap, Tripper = harpy };
                entMan.EventBus.RaiseLocalEvent(trap, ref harpyEv);
                Assert.That(harpyEv.Cancelled, Is.True);

                var humanEv = new StepTriggerAttemptEvent { Source = trap, Tripper = human };
                entMan.EventBus.RaiseLocalEvent(trap, ref humanEv);
                Assert.That(humanEv.Cancelled, Is.False);
            }
        });
    }

    [Test]
    public async Task HarpyKeepsFlightAfterDisposals()
    {
        var server = Pair.Server;
        var entMan = server.EntMan;
        var holderSys = server.System<DisposalHolderSystem>();
        var container = server.System<SharedContainerSystem>();
        var actions = server.System<SharedActionsSystem>();
        var testMap = await Pair.CreateTestMap();

        EntityUid harpy = default;
        EntityUid holder = default;

        await server.WaitAssertion(() =>
        {
            harpy = entMan.SpawnEntity("MobHarpy", testMap.GridCoords);
            holder = entMan.SpawnEntity("DisposalHolder", testMap.GridCoords);

            var holderComp = entMan.GetComponent<DisposalHolderComponent>(holder);
            Assert.That(container.Insert(harpy, holderComp.Container!));
            holderSys.AttachEntity((holder, holderComp), harpy);
            holderSys.Exit((holder, holderComp));
        });

        await Pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            var flight = entMan.GetComponent<FlightComponent>(harpy);
            Assert.That(flight.ToggleActionEntity, Is.Not.Null);
            Assert.That(entMan.EntityExists(flight.ToggleActionEntity));
            Assert.That(actions.GetActions(harpy).Select(a => a.Owner), Does.Contain(flight.ToggleActionEntity!.Value));

            server.System<SharedFlightSystem>().SetFlying((harpy, flight), true);
            Assert.That(flight.On);
        });
    }
}
