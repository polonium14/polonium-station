using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Steps;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared.Body;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Surgery;

[TestFixture]
public sealed class LobotomizeStepCompletionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: LobotomizeStepTestVictim
  components:
  - type: Body

- type: entity
  id: LobotomizeStepTestBrain
  components:
  - type: Organ
    category: Brain
";

    [Test]
    public async Task LobotomizeStepIsNotCompleteUntilDrilled()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sSurgery = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SurgerySystem>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid victim = default;
        EntityUid brain = default;

        await server.WaitPost(() =>
        {
            victim = sEntMan.SpawnEntity("LobotomizeStepTestVictim", coords);
            brain = sEntMan.SpawnEntity("LobotomizeStepTestBrain", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(brain, organsContainer);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var stepEnt = sSurgery.GetSingleton("SurgeryStepLobotomize");
            Assert.That(stepEnt, Is.Not.Null, "SurgeryStepLobotomize should resolve to a real singleton entity.");

            var surgeryEnt = sSurgery.GetSingleton("SurgeryLobotomize");
            var ev = new SurgeryStepCompleteCheckEvent(victim, brain, surgeryEnt!.Value);
            sEntMan.EventBus.RaiseLocalEvent(stepEnt!.Value, ref ev);

            Assert.That(ev.Cancelled, Is.True,
                "The lobotomize step should NOT be complete before the drill has ever been used on this brain - it was previously always reporting complete instantly, matching the reported bug.");
        });

        // Simulates what AddOrRemoveComponentsToEntity does once the drill DoAfter succeeds.
        await server.WaitPost(() =>
        {
            sEntMan.EnsureComponent<LobotomizedComponent>(brain);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var stepEnt = sSurgery.GetSingleton("SurgeryStepLobotomize");
            var surgeryEnt = sSurgery.GetSingleton("SurgeryLobotomize");
            var ev = new SurgeryStepCompleteCheckEvent(victim, brain, surgeryEnt!.Value);
            sEntMan.EventBus.RaiseLocalEvent(stepEnt!.Value, ref ev);

            Assert.That(ev.Cancelled, Is.False,
                "Once the drill has marked the brain as Lobotomized, the step should report complete.");
        });
    }

    [Test]
    public async Task MendBrainTissueStepIsNotCompleteWhileLobotomized()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sSurgery = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SurgerySystem>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid victim = default;
        EntityUid brain = default;

        await server.WaitPost(() =>
        {
            victim = sEntMan.SpawnEntity("LobotomizeStepTestVictim", coords);
            brain = sEntMan.SpawnEntity("LobotomizeStepTestBrain", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(brain, organsContainer);

            // Simulates a brain that was already drilled by a prior lobotomy.
            sEntMan.EnsureComponent<LobotomizedComponent>(brain);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var stepEnt = sSurgery.GetSingleton("SurgeryStepMendBrainTissue");
            var surgeryEnt = sSurgery.GetSingleton("SurgeryMendBrainTissue");
            var ev = new SurgeryStepCompleteCheckEvent(victim, brain, surgeryEnt!.Value);
            sEntMan.EventBus.RaiseLocalEvent(stepEnt!.Value, ref ev);

            Assert.That(ev.Cancelled, Is.True,
                "Mending brain tissue on a still-lobotomized brain shouldn't be complete yet.");
        });

        await server.WaitPost(() =>
        {
            sEntMan.RemoveComponent<LobotomizedComponent>(brain);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var stepEnt = sSurgery.GetSingleton("SurgeryStepMendBrainTissue");
            var surgeryEnt = sSurgery.GetSingleton("SurgeryMendBrainTissue");
            var ev = new SurgeryStepCompleteCheckEvent(victim, brain, surgeryEnt!.Value);
            sEntMan.EventBus.RaiseLocalEvent(stepEnt!.Value, ref ev);

            Assert.That(ev.Cancelled, Is.False,
                "Once the Lobotomized marker is cleared (hemostat step performed), mending should report complete.");
        });
    }
}
