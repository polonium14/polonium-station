using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Content.Shared.FixedPoint;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

/// <summary>
/// Production crash (recurrence of the same "Can't resolve MetaDataComponent" PVS
/// serialization error on a different entity, after the surgical-organ-removal variant of this
/// bug was fixed - see OrganRemovalTraumaCleanupTest): an OrganDamage trauma can be induced by a
/// wound on ANY limb but targets a RANDOM vital organ anywhere on the body
/// (TraumaSystem.Process.cs's ApplyTraumas picks from every organ with OrganIntegrityComponent,
/// not scoped to the wounded limb) - so a wound held by one limb can carry a TraumaTarget
/// pointing at a completely different, unrelated vital organ. Once that limb is dismembered
/// (relocated as a loose item, not deleted), it's no longer reachable via
/// body.Organs.ContainedEntities - so if the target vital organ is later destroyed or removed
/// by anything that tries to clean up traumas by walking the body's own organ list, it can't
/// find this limb's wound at all, and the dangling reference crashes PVS state serialization
/// once the limb itself is eventually deleted.
///
/// Rather than special-casing every removal/detachment path, TraumaSystem.Cleanup.cs cleans up
/// stale traumas generically whenever any organ/bone/woundable actually terminates - triggered
/// here by deleting the TARGET organ (the heart), which the generic hook finds via a body-wide
/// sweep regardless of where the referencing trauma physically lives.
/// </summary>
[TestFixture]
[TestOf(typeof(TraumaSystem))]
public sealed class DismemberedLimbTraumaCleanupTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: DismemberTraumaTestBody
  components:
  - type: Body

- type: entity
  id: DismemberTraumaTestArm
  components:
  - type: Organ
    category: ArmLeft
  - type: Woundable
    integrityCap: 80
    thresholds:
      Healthy: 80
      Minor: 64
      Moderate: 48
      Severe: 32
      Critical: 16
      Mangled: 6
      Severed: 0

- type: entity
  id: DismemberTraumaTestHeart
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
  id: DismemberTraumaTestWound
  components:
  - type: Wound
    damageType: Blunt
  - type: TraumaInflicter
    allowedTraumas:
    - OrganDamage
";

    [Test]
    public async Task DismemberedLimbsTraumaIsCleanedUpWhenItsTargetOrganIsDeleted()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sTrauma = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<TraumaSystem>();
        var sWound = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<WoundSystem>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid body = default;
        EntityUid arm = default;
        EntityUid heart = default;
        EntityUid wound = default;
        EntityUid traumaEnt = default;

        await server.WaitPost(() =>
        {
            body = sEntMan.SpawnEntity("DismemberTraumaTestBody", coords);
            arm = sEntMan.SpawnEntity("DismemberTraumaTestArm", coords);
            heart = sEntMan.SpawnEntity("DismemberTraumaTestHeart", coords);
            wound = sEntMan.SpawnEntity("DismemberTraumaTestWound", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(body, BodyComponent.ContainerID);
            container.Insert(arm, organsContainer);
            container.Insert(heart, organsContainer);

            // The arm's own wound carries a trauma that targets the (unrelated) heart - the
            // real mechanic's own cross-limb randomness, not a test artifact.
            var woundsContainer = container.GetContainer(arm, WoundableComponent.WoundContainerId);
            container.Insert(wound, woundsContainer);

            var woundableComp = sEntMan.GetComponent<WoundableComponent>(arm);
            var woundInflicterComp = sEntMan.GetComponent<TraumaInflicterComponent>(wound);
            traumaEnt = sTrauma.AddTrauma(heart, (arm, woundableComp), (wound, woundInflicterComp), TraumaType.OrganDamage, FixedPoint2.New(20));
        });

        await pair.RunTicksSync(5);

        await server.WaitPost(() => sWound.DestroyWoundable(arm));

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(body, BodyComponent.ContainerID);
            Assert.That(organsContainer.ContainedEntities, Does.Not.Contain(arm), "Sanity check: the arm should have actually been dismembered off the body.");
            Assert.That(sEntMan.Deleted(traumaEnt), Is.False,
                "Dismembering the arm alone shouldn't clean up its trauma yet - the target organ (heart) is still perfectly valid.");
        });

        // The heart is still attached to the body (unaffected by the arm's dismemberment) -
        // simulate it later being destroyed/removed by whatever means.
        await server.WaitPost(() => sEntMan.DeleteEntity(heart));

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(sEntMan.Deleted(traumaEnt), Is.True,
                "Once the heart is deleted, the trauma the (now-detached, still-existing) arm was holding should be cleaned up too, not left dangling on an unreachable limb.");
        });
    }
}
