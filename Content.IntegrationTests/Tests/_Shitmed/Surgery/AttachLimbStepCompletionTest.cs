// SPDX-FileCopyrightText: 2026 Maciej Walendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Shitmed.Medical.Surgery;
using Content.Shared.Body;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Surgery;

/// <summary>
/// Live playtest report: "amputation works but i cannot reattach - the step is marked as
/// green." Root cause: SharedSurgerySystem.Steps.cs's OnAddPartCheck checked
/// `args.Part` (the body part the player TARGETS the surgery on - Torso, per
/// SurgeryAttachLeftArm's SurgeryPartCondition) against `args.Body`, i.e.
/// "is Torso still attached to its own body" - which is always true, so the step
/// unconditionally reported complete before the severed limb was ever reinserted. Fixed to
/// match OnAddOrganCheck's actually-correct pattern: check whether the CATEGORY that was
/// supposed to be reattached (SurgeryPartRemovedCondition, the same field OnAddPartStep
/// itself matches the held tool organ against) now resolves to a real organ on the body at
/// all, via LimbTargetMap.
/// </summary>
[TestFixture]
public sealed class AttachLimbStepCompletionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: AttachLimbStepTestVictim
  components:
  - type: Body

- type: entity
  id: AttachLimbStepTestTorsoOrgan
  components:
  - type: Organ
    category: Torso

- type: entity
  id: AttachLimbStepTestArmOrgan
  components:
  - type: Organ
    category: ArmLeft
";

    [Test]
    public async Task InsertFeatureStepIsNotCompleteUntilTheLimbIsReattached()
    {
        var pair = Pair;
        var server = pair.Server;

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var sSurgery = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SurgerySystem>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        EntityUid victim = default;
        EntityUid torso = default;
        EntityUid arm = default;

        await server.WaitPost(() =>
        {
            victim = sEntMan.SpawnEntity("AttachLimbStepTestVictim", coords);
            torso = sEntMan.SpawnEntity("AttachLimbStepTestTorsoOrgan", coords);
            // The severed arm - deliberately NOT inserted into the body yet, matching a
            // freshly-amputated limb the player is holding and about to reattach.
            arm = sEntMan.SpawnEntity("AttachLimbStepTestArmOrgan", coords);

            var container = sEntMan.System<SharedContainerSystem>();
            var organsContainer = container.GetContainer(victim, BodyComponent.ContainerID);
            container.Insert(torso, organsContainer);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var stepEnt = sSurgery.GetSingleton("SurgeryStepInsertFeature");
            Assert.That(stepEnt, Is.Not.Null, "SurgeryStepInsertFeature should resolve to a real singleton entity.");

            var surgeryEnt = sSurgery.GetSingleton("SurgeryAttachLeftArm");
            Assert.That(surgeryEnt, Is.Not.Null, "SurgeryAttachLeftArm should resolve to a real singleton entity.");

            // args.Part is the Torso (the body part the player targets this surgery on) -
            // the previous bug checked Torso's own attachment, which is always true.
            Assert.That(sSurgery.IsStepComplete(victim, torso, "SurgeryStepInsertFeature", surgeryEnt!.Value), Is.False,
                "The reattach step should NOT be complete before the arm has actually been reinserted - it was previously always reporting complete instantly, matching the reported bug.");
        });

        // Simulates what OnAddPartStep does once the player actually uses the held arm on
        // the open incision.
        await server.WaitPost(() =>
        {
            var container = sEntMan.System<SharedContainerSystem>();
            var body = sEntMan.GetComponent<BodyComponent>(victim);
            container.Insert(arm, body.Organs!);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var surgeryEnt = sSurgery.GetSingleton("SurgeryAttachLeftArm");
            Assert.That(sSurgery.IsStepComplete(victim, torso, "SurgeryStepInsertFeature", surgeryEnt!.Value), Is.True,
                "Once the arm is actually back in the body's Organs container, the step should report complete.");
        });
    }
}
