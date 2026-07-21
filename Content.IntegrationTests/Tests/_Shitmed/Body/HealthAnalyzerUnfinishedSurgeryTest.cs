using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.Medical;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

[TestFixture]
[TestOf(typeof(HealthAnalyzerSystem))]
public sealed class HealthAnalyzerUnfinishedSurgeryTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: UnfinishedSurgerySelf
  components:
  - type: Body
  - type: Damageable
  - type: Injurable
  - type: Consciousness
    threshold: 95
    cap: 190
  - type: Targeting

- type: entity
  id: UnfinishedSurgeryArm
  components:
  - type: Organ
    category: ArmLeft
  - type: Damageable
  - type: Injurable
  - type: Nerve
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
  id: UnfinishedSurgeryLeg
  components:
  - type: Organ
    category: LegLeft
  - type: Damageable
  - type: Injurable
  - type: Nerve
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
";

    [Test]
    public async Task FetchUnfinishedSurgeryDataReportsOpenIncision()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sHealthAnalyzer = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<HealthAnalyzerSystem>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid arm = default;
        EntityUid leg = default;
        BodyComponent bodyComp = default!;

        await server.WaitPost(() =>
        {
            var self = sEntMan.SpawnEntity("UnfinishedSurgerySelf", coords);
            arm = sEntMan.SpawnEntity("UnfinishedSurgeryArm", coords);
            leg = sEntMan.SpawnEntity("UnfinishedSurgeryLeg", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(self, BodyComponent.ContainerID);
            container.Insert(arm, organsContainer);
            container.Insert(leg, organsContainer);

            bodyComp = sEntMan.GetComponent<BodyComponent>(self);
        });

        await pair.RunTicksSync(5);

        var fetchMethod = typeof(HealthAnalyzerSystem).GetMethod("FetchUnfinishedSurgeryData", BindingFlags.Instance | BindingFlags.NonPublic)!;

        await server.WaitAssertion(() =>
        {
            var before = (Dictionary<TargetBodyPart, bool>) fetchMethod.Invoke(sHealthAnalyzer, new object[] { bodyComp })!;
            Assert.That(before[TargetBodyPart.LeftArm], Is.False, "Arm shouldn't show as unfinished before any surgery marker is added.");
            Assert.That(before[TargetBodyPart.LeftLeg], Is.False, "Leg shouldn't show as unfinished - it was never touched.");
        });

        // Open an incision on the arm - the yml `add:`/`remove:`-driven marker code path.
        await server.WaitPost(() => sEntMan.AddComponent<IncisionOpenComponent>(arm));
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var after = (Dictionary<TargetBodyPart, bool>) fetchMethod.Invoke(sHealthAnalyzer, new object[] { bodyComp })!;
            Assert.That(after[TargetBodyPart.LeftArm], Is.True, "The arm has an open incision - it should show as unfinished.");
            Assert.That(after[TargetBodyPart.LeftLeg], Is.False, "The untouched leg should still show as finished.");
        });

        // Close it back up - should flip back to finished.
        await server.WaitPost(() => sEntMan.RemoveComponent<IncisionOpenComponent>(arm));
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var closed = (Dictionary<TargetBodyPart, bool>) fetchMethod.Invoke(sHealthAnalyzer, new object[] { bodyComp })!;
            Assert.That(closed[TargetBodyPart.LeftArm], Is.False, "Closing the incision should clear the unfinished flag.");
        });

        // BodyPartReattachedComponent is added directly in C# (OnAddPartStep), not via a yml
        // add:/remove: list - covers the other marker code path.
        await server.WaitPost(() => sEntMan.AddComponent<BodyPartReattachedComponent>(leg));
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var reattached = (Dictionary<TargetBodyPart, bool>) fetchMethod.Invoke(sHealthAnalyzer, new object[] { bodyComp })!;
            Assert.That(reattached[TargetBodyPart.LeftLeg], Is.True, "A freshly-reattached-but-not-affixed leg should show as unfinished.");
            Assert.That(reattached[TargetBodyPart.LeftArm], Is.False, "The arm's own (now closed) incision shouldn't bleed into the leg's status.");
        });
    }
}
