using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared.Body;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

[TestFixture]
[TestOf(typeof(SharedSurgerySystem))]
public sealed class AttachLimbSurgeryReattachGraceTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: ReattachGraceMob
  components:
  - type: Body

- type: entity
  id: ReattachGraceArm
  components:
  - type: Organ
    category: ArmLeft

- type: entity
  id: ReattachGraceSurgery
  components:
  - type: SurgeryPartRemovedCondition
    category: ArmLeft
";

    [Test]
    public async Task PartRemovedConditionStaysValidWhileLimbIsFreshlyReattached()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid mob = default;
        EntityUid arm = default;
        EntityUid surgery = default;

        await server.WaitPost(() =>
        {
            mob = sEntMan.SpawnEntity("ReattachGraceMob", coords);
            arm = sEntMan.SpawnEntity("ReattachGraceArm", coords);
            surgery = sEntMan.SpawnEntity("ReattachGraceSurgery", coords);
        });

        await pair.RunTicksSync(5);

        bool Validate()
        {
            var ev = new SurgeryValidEvent(mob, mob);
            sEntMan.EventBus.RaiseLocalEvent(surgery, ref ev);
            return !ev.Cancelled;
        }

        await server.WaitAssertion(() =>
        {
            Assert.That(Validate(), Is.True,
                "With the arm missing, the attach surgery's part-removed condition should be valid.");
        });

        await server.WaitPost(() =>
        {
            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(mob, BodyComponent.ContainerID);
            container.Insert(arm, organsContainer);
            sEntMan.AddComponent<BodyPartReattachedComponent>(arm);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(Validate(), Is.True,
                "With the limb inserted but still tagged reattached, the surgery must stay valid - otherwise the affix step is unreachable and the reattached marker (and its bleed penalty) never clears.");
        });

        await server.WaitPost(() =>
        {
            var step = sEntMan.SpawnEntity("SurgeryStepSealWounds", coords);
            var ev = new SurgeryStepEvent(mob, mob, mob, mob, surgery, step);
            sEntMan.EventBus.RaiseLocalEvent(step, ref ev);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(sEntMan.HasComponent<BodyPartReattachedComponent>(arm), Is.False,
                "The affix step should remove the reattached marker from the limb resolved via the surgery's part-removed category.");
            Assert.That(Validate(), Is.False,
                "With the limb attached and the marker cleared, the attach surgery should no longer be valid.");
        });
    }
}
