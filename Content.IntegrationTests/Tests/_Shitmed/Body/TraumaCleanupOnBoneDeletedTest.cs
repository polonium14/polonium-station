using System.Numerics;
using Content.IntegrationTests.Fixtures;
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

namespace Content.IntegrationTests.Tests._Shitmed.Body;

/// <summary>
/// Proactive coverage for TraumaSystem.Cleanup.cs's generic stale-trauma cleanup, added after
/// fixing the same "dangling TraumaTarget crashes PVS serialization" bug twice in a row for two
/// different removal paths (surgical organ removal, limb dismemberment - see
/// OrganRemovalTraumaCleanupTest/DismemberedLimbTraumaCleanupTest). Rather than keep patching
/// each new way an organ/bone/woundable can disappear, the generic hook subscribes on
/// EntityTerminatingEvent for all three trauma-target-capable component types and sweeps every
/// trauma on actual deletion, regardless of cause. This test covers the one target kind neither
/// of the two prior one-off fixes ever touched: a BoneDamage trauma's TraumaTarget (the bone
/// entity itself) being deleted directly, with no removal step involved at all.
/// </summary>
[TestFixture]
[TestOf(typeof(TraumaSystem))]
public sealed class TraumaCleanupOnBoneDeletedTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: BoneCleanupTestBody
  components:
  - type: Body

- type: entity
  id: BoneCleanupTestTorso
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
  id: BoneCleanupTestBone
  components:
  - type: Bone

- type: entity
  id: BoneCleanupTestWound
  components:
  - type: Wound
    damageType: Blunt
  - type: TraumaInflicter
    allowedTraumas:
    - BoneDamage
";

    [Test]
    public async Task DeletingABoneDirectlyCleansUpTraumasTargetingIt()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sTrauma = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<TraumaSystem>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid body = default;
        EntityUid torso = default;
        EntityUid bone = default;
        EntityUid wound = default;
        EntityUid traumaEnt = default;

        await server.WaitPost(() =>
        {
            body = sEntMan.SpawnEntity("BoneCleanupTestBody", coords);
            torso = sEntMan.SpawnEntity("BoneCleanupTestTorso", coords);
            bone = sEntMan.SpawnEntity("BoneCleanupTestBone", coords);
            wound = sEntMan.SpawnEntity("BoneCleanupTestWound", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(body, BodyComponent.ContainerID);
            container.Insert(torso, organsContainer);

            var woundsContainer = container.GetContainer(torso, WoundableComponent.WoundContainerId);
            container.Insert(wound, woundsContainer);

            var woundableComp = sEntMan.GetComponent<WoundableComponent>(torso);
            var woundInflicterComp = sEntMan.GetComponent<TraumaInflicterComponent>(wound);
            traumaEnt = sTrauma.AddTrauma(bone, (torso, woundableComp), (wound, woundInflicterComp), TraumaType.BoneDamage, FixedPoint2.New(20));
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(sEntMan.Deleted(traumaEnt), Is.False, "Sanity check: the trauma should exist before the bone is deleted.");
        });

        await server.WaitPost(() => sEntMan.DeleteEntity(bone));

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(sEntMan.Deleted(traumaEnt), Is.True,
                "Deleting the bone a BoneDamage trauma targets should clean up that trauma too, not leave it dangling with a stale TraumaTarget reference.");
        });
    }
}
