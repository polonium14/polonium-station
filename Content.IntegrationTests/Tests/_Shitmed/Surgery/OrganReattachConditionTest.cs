using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared.Body;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Surgery;

[TestFixture]
public sealed class OrganReattachConditionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: OrganReattachTestVictim
  components:
  - type: Body

- type: entity
  id: OrganReattachTestOrgan
  components:
  - type: Organ
    category: Heart

- type: entity
  id: OrganReattachTestSurgery
  components:
  - type: SurgeryOrganCondition
    category: Heart
    inverse: true
    reattaching: true
";

    [Test]
    public async Task StaysValidWhileFreshlyReattachedTagPresent()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid victim = default;
        EntityUid organ = default;
        EntityUid surgery = default;

        await server.WaitPost(() =>
        {
            victim = sEntMan.SpawnEntity("OrganReattachTestVictim", coords);
            organ = sEntMan.SpawnEntity("OrganReattachTestOrgan", coords);
            surgery = sEntMan.SpawnEntity("OrganReattachTestSurgery", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(organ, organsContainer);

            // Matches OnAddOrganStep's own EnsureComp call the instant an organ is inserted.
            sEntMan.EnsureComponent<OrganReattachedComponent>(organ);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var ev = new SurgeryValidEvent(victim, organ);
            sEntMan.EventBus.RaiseLocalEvent(surgery, ref ev);

            Assert.That(ev.Cancelled, Is.False,
                "Surgery invalidated itself the instant the organ was tagged as freshly reattached - the grace period for the remaining steps (e.g. SealOrganWound) never happens.");
        });

        // Simulates the affix/seal step clearing the tag once the surgery is actually finished.
        await server.WaitPost(() =>
        {
            sEntMan.RemoveComponent<OrganReattachedComponent>(organ);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var ev = new SurgeryValidEvent(victim, organ);
            sEntMan.EventBus.RaiseLocalEvent(surgery, ref ev);

            Assert.That(ev.Cancelled, Is.True,
                "Surgery should invalidate once the freshly-reattached tag is cleared (organ fully attached, no longer eligible for the insert surgery) - the grace period should be one step, not permanent.");
        });
    }
}
