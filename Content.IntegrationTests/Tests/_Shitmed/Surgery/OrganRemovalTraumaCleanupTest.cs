using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Steps;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared.Body;
using Content.Shared.FixedPoint;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Surgery;

/// <summary>
/// Production crash: "Can't resolve MetaDataComponent" on an entity during PVS state
/// serialization, traced to TraumaComponent's auto-networked TraumaTarget field pointing at a
/// deleted entity. Root cause: SharedSurgerySystem.Steps.cs's OnRemoveOrganStep pulls a damaged
/// organ out of the body (relocated into the surgeon's hand, not deleted) without touching any
/// TraumaComponent still targeting it via TraumaTarget - so once the removed organ was later
/// deleted by any other means (dropped and culled, disposed, etc.), the dangling TraumaTarget
/// crashed the next PVS state sync for the still-existing wound's TraumaComponent.
///
/// Rather than special-casing this one removal path, TraumaSystem.Cleanup.cs now cleans up
/// stale traumas generically whenever any organ/bone/woundable actually terminates, regardless
/// of what caused it. This test confirms both halves: removing the organ alone must NOT
/// prematurely clean up its trauma (it's still a perfectly valid, undeleted entity at that
/// point), and the trauma must be cleaned up once the organ is later actually deleted.
/// </summary>
[TestFixture]
[TestOf(typeof(TraumaSystem))]
public sealed class OrganRemovalTraumaCleanupTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: OrganRemovalTraumaTestBody
  components:
  - type: Body

- type: entity
  id: OrganRemovalTraumaTestTorso
  components:
  - type: Organ
    category: Torso
  - type: Woundable
    integrityCap: 200
    thresholds:
      Healthy: 200
      Minor: 160
      Moderate: 120
      Severe: 80
      Critical: 40
      Mangled: 14
      Severed: 0

- type: entity
  id: OrganRemovalTraumaTestHeart
  components:
  - type: Organ
    category: Heart
  - type: OrganIntegrity
    integrityCap: 15
    integrityThresholds:
      Normal: 15
      Damaged: 6
      Destroyed: 0

- type: entity
  id: OrganRemovalTraumaTestWound
  components:
  - type: Wound
    damageType: Blunt
  - type: TraumaInflicter
    allowedTraumas:
    - OrganDamage

- type: entity
  id: OrganRemovalTraumaTestSurgery
  components:
  - type: SurgeryOrganCondition
    category: Heart

- type: entity
  id: OrganRemovalTraumaTestStep
  components:
  - type: SurgeryRemoveOrganStep
";

    [Test]
    public async Task RemovedOrganKeepsItsTraumaUntilActuallyDeleted()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sTrauma = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<TraumaSystem>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid user = default;
        EntityUid body = default;
        EntityUid torso = default;
        EntityUid heart = default;
        EntityUid wound = default;
        EntityUid surgery = default;
        EntityUid step = default;
        EntityUid traumaEnt = default;

        await server.WaitPost(() =>
        {
            user = sEntMan.SpawnEntity(null, coords);
            body = sEntMan.SpawnEntity("OrganRemovalTraumaTestBody", coords);
            torso = sEntMan.SpawnEntity("OrganRemovalTraumaTestTorso", coords);
            heart = sEntMan.SpawnEntity("OrganRemovalTraumaTestHeart", coords);
            wound = sEntMan.SpawnEntity("OrganRemovalTraumaTestWound", coords);
            surgery = sEntMan.SpawnEntity("OrganRemovalTraumaTestSurgery", coords);
            step = sEntMan.SpawnEntity("OrganRemovalTraumaTestStep", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(body, BodyComponent.ContainerID);
            container.Insert(torso, organsContainer);
            container.Insert(heart, organsContainer);

            // GetWoundableWounds (which TryGetBodyTraumas/TryGetWoundableTrauma walk to find
            // traumas) only looks inside the woundable's own Wounds container - the wound must
            // actually be inserted there, not just passed as a loose argument to AddTrauma.
            var woundsContainer = container.GetContainer(torso, WoundableComponent.WoundContainerId);
            container.Insert(wound, woundsContainer);

            var woundableComp = sEntMan.GetComponent<WoundableComponent>(torso);
            var woundInflicterComp = sEntMan.GetComponent<TraumaInflicterComponent>(wound);
            traumaEnt = sTrauma.AddTrauma(heart, (torso, woundableComp), (wound, woundInflicterComp), TraumaType.OrganDamage, FixedPoint2.New(20));
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var ev = new SurgeryStepEvent(user, body, torso, EntityUid.Invalid, surgery, step);
            sEntMan.EventBus.RaiseLocalEvent(step, ref ev);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(body, BodyComponent.ContainerID);
            Assert.That(organsContainer.ContainedEntities, Does.Not.Contain(heart), "The heart should have actually been removed from the body.");
            Assert.That(sEntMan.Deleted(traumaEnt), Is.False,
                "The heart is still a perfectly valid, undeleted entity right after removal - the trauma referencing it shouldn't be cleaned up yet.");
        });

        // Simulate the removed heart eventually being deleted by whatever means (dropped and
        // culled, thrown in disposal, etc.) - this is the actual moment the crash used to
        // happen at.
        await server.WaitPost(() => sEntMan.DeleteEntity(heart));

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(sEntMan.Deleted(traumaEnt), Is.True,
                "Once the removed heart is actually deleted, the trauma that was targeting it should be cleaned up too, not left dangling with a stale TraumaTarget reference.");
        });
    }
}
