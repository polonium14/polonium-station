using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Body;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared.Body;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Surgery;

[TestFixture]
public sealed class SurgeryInsertionToolTest : GameTest
{
    [TestCase("SurgeryInsertLiver", "SurgeryStepInsertLiver", "OrganHumanHeart", false)]
    [TestCase("SurgeryInsertLiver", "SurgeryStepInsertLiver", "OrganHumanLiver", true)]
    [TestCase("SurgeryAttachLeftArm", "SurgeryStepInsertArmLeft", "OrganHumanArmRight", false)]
    [TestCase("SurgeryAttachLeftArm", "SurgeryStepInsertArmLeft", "OrganHumanArmLeft", true)]
    public async Task InsertionRequiresTheCorrectCategory(string procedure, string step, string tool, bool expected)
    {
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);
        await Server.WaitAssertion(() =>
        {
            var patient = SEntMan.SpawnEntity("TendWoundsStepTestVictim", coords);
            var torso = SEntMan.SpawnEntity("TendWoundsStepTestTorsoOrgan", coords);
            var user = SEntMan.SpawnEntity(null, coords);
            SEntMan.AddComponent<HandsComponent>(user);
            SEntMan.AddComponent<DoAfterComponent>(user);
            var hands = SEntMan.System<SharedHandsSystem>();
            hands.AddHand(user, "right", HandLocation.Right);
            var heart = SEntMan.SpawnEntity(tool, coords);
            Assert.That(hands.TryPickupAnyHand(user, heart), Is.True);
            var containers = SEntMan.System<SharedContainerSystem>();
            containers.Insert(torso, containers.GetContainer(patient, BodyComponent.ContainerID));
            SEntMan.AddComponent<IncisionOpenComponent>(torso);
            SEntMan.AddComponent<SkinRetractedComponent>(torso);
            Assert.That(SEntMan.System<SurgerySystem>().TryDoSurgeryStep(patient, torso, user,
                procedure, step, out var error), Is.EqualTo(expected),
                "Holding any Organ component should not pass tool validation for the wrong transplant category.");
            if (!expected)
                Assert.That(error, Is.EqualTo(StepInvalidReason.ToolInvalid));
        });
    }
}
