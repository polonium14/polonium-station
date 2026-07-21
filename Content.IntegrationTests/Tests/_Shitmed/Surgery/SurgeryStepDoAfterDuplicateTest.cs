using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared.Body;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Shitmed.Surgery;

[TestFixture]
[TestOf(typeof(SurgerySystem))]
public sealed class SurgeryStepDoAfterDuplicateTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: DupTestSurgery
  components:
  - type: Surgery
    steps:
    - DupTestStep

- type: entity
  id: DupTestStep
  components:
  - type: SurgeryStep
    duration: 5
    add:
    - type: IncisionOpen

- type: entity
  id: DupTestUser
  components:
  - type: Hands
  - type: DoAfter

- type: entity
  id: DupTestBody
  components:
  - type: Body
  - type: SurgeryTarget

- type: entity
  id: DupTestArm
  components:
  - type: Organ
    category: ArmLeft
";

    [Test]
    public async Task StartingAStepOnADifferentOrganDoesNotCancelTheFirst()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sSurgery = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SurgerySystem>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid user = default;
        EntityUid body = default;
        EntityUid leftArm = default;
        EntityUid rightArm = default;

        await server.WaitPost(() =>
        {
            user = sEntMan.SpawnEntity("DupTestUser", coords);
            body = sEntMan.SpawnEntity("DupTestBody", coords);
            leftArm = sEntMan.SpawnEntity("DupTestArm", coords);
            rightArm = sEntMan.SpawnEntity("DupTestArm", coords);

            sEntMan.System<SharedHandsSystem>().AddHand(user, "right", HandLocation.Right);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(body, BodyComponent.ContainerID);
            container.Insert(leftArm, organsContainer);
            container.Insert(rightArm, organsContainer);
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var startedLeft = sSurgery.TryDoSurgeryStep(body, leftArm, user, "DupTestSurgery", "DupTestStep", out var error1);
            Assert.That(startedLeft, Is.True, $"Sanity check: the first step should start ({error1}).");

            var startedRight = sSurgery.TryDoSurgeryStep(body, rightArm, user, "DupTestSurgery", "DupTestStep", out var error2);
            Assert.That(startedRight, Is.True, $"Sanity check: the second step (different organ) should also start ({error2}).");
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var doAfterComp = sEntMan.GetComponent<DoAfterComponent>(user);

            var surgeryDoAfters = 0;
            var anyCancelled = false;
            foreach (var doAfter in doAfterComp.DoAfters.Values)
            {
                if (doAfter.Args.Event is not SurgeryDoAfterEvent)
                    continue;

                surgeryDoAfters++;
                if (doAfter.Cancelled)
                    anyCancelled = true;
            }

            Assert.That(surgeryDoAfters, Is.EqualTo(2), "Both surgery step DoAfters should still be tracked.");
            Assert.That(anyCancelled, Is.False,
                "Starting a step on the right arm should NOT have cancelled the left arm's in-progress step - they're different organs, not a real duplicate.");
        });
    }
}
