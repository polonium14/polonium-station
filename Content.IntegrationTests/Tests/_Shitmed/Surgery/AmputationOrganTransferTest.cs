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
public sealed class AmputationOrganTransferTest : GameTest
{
    [TestCase("Head", "Brain", "SurgeryAttachHead", "SurgeryStepInsertHead")]
    [TestCase("ArmLeft", "HandLeft", "SurgeryAttachLeftArm", "SurgeryStepInsertArmLeft")]
    public async Task DescendantsTravelWithThePartAndReturnOnReattachment(string partCategory, string childCategory, string attachProcedure, string insertStep)
    {
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);
        await Server.WaitAssertion(() =>
        {
            var patient = SEntMan.SpawnEntity("MobHuman", coords);
            var body = SEntMan.GetComponent<BodyComponent>(patient);
            Assert.That(LimbTargetMap.TryGetOrganByCategory(SEntMan, body, partCategory, out var head), Is.True);
            Assert.That(LimbTargetMap.TryGetOrganByCategory(SEntMan, body, childCategory, out var brain), Is.True);
            SEntMan.GetComponent<SurgeryTargetComponent>(patient).SepsisImmune = true;
            var tool = SEntMan.SpawnEntity("Saw", coords);
            var surgery = SEntMan.System<SurgerySystem>();
            var step = surgery.GetSingleton("SurgeryStepRemoveFeature")!.Value;
            var ev = new SurgeryStepEvent(patient, patient, head, tool, surgery.GetSingleton("SurgeryRemovePart")!.Value, step);
            SEntMan.EventBus.RaiseLocalEvent(step, ref ev);
            Assert.That(SEntMan.GetComponent<OrganComponent>(head).Body, Is.Null, "Confirm the head was actually removed.");
            Assert.That(SEntMan.GetComponent<OrganComponent>(brain).Body, Is.Not.EqualTo(patient),
                "Removing a head must not leave its brain attached to the original body.");
            var containers = SEntMan.System<SharedContainerSystem>();
            Assert.That(containers.GetContainer(head, DismemberedPartComponent.ContainerId).ContainedEntities, Does.Contain(brain));
            Assert.That(SEntMan.GetComponent<ChildOrganComponent>(head).Parent, Is.Null);
            Assert.That(LimbTargetMap.TryGetOrganByCategory(SEntMan, body, "Torso", out var torso), Is.True);
            var insert = surgery.GetSingleton(insertStep)!.Value;
            ev = new SurgeryStepEvent(patient, patient, torso, head, surgery.GetSingleton(attachProcedure)!.Value, insert);
            SEntMan.EventBus.RaiseLocalEvent(insert, ref ev);
            Assert.That(SEntMan.GetComponent<OrganComponent>(brain).Body, Is.EqualTo(patient));
            Assert.That(SEntMan.GetComponent<OrganComponent>(head).Body, Is.EqualTo(patient));
            Assert.That(SEntMan.GetComponent<ChildOrganComponent>(head).Parent, Is.EqualTo(torso));
            Assert.That(SEntMan.GetComponent<ChildOrganComponent>(brain).Parent, Is.EqualTo(head));
        });
    }

}
