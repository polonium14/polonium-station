using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared.Body;
using Content.Shared.FixedPoint;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Shitmed.Surgery;

[TestFixture]
public sealed class SurgeryBlockedUiTest : GameTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task IncisionDoesNotExposeTraumaBlockedAttachment(bool incisionOpen)
    {
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);
        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.SpawnEntity("TendWoundsStepTestVictim", coords);
            var part = SEntMan.SpawnEntity("TendWoundsStepTestTorsoOrgan", coords);
            var wound = SEntMan.SpawnEntity("HealedTraumaWound", coords);
            var containers = SEntMan.System<SharedContainerSystem>();
            containers.Insert(part, containers.GetContainer(body, BodyComponent.ContainerID));
            containers.Insert(wound, containers.GetContainer(part, WoundableComponent.WoundContainerId));
            if (incisionOpen)
                SEntMan.AddComponent<IncisionOpenComponent>(part);

            var surgery = SEntMan.System<SurgerySystem>();
            var refresh = typeof(SurgerySystem).GetMethod("RefreshUI", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var field = typeof(SurgerySystem).GetField("_surgeries", BindingFlags.NonPublic | BindingFlags.Instance)!;
            bool AttachmentListed()
            {
                refresh.Invoke(surgery, new object[] { body });
                var entries = (Dictionary<NetEntity, List<EntProtoId>>)field.GetValue(surgery)!;
                return entries[SEntMan.GetNetEntity(part)].Contains("SurgeryAttachLeftArm");
            }

            Assert.That(AttachmentListed(), Is.True, "A missing arm should offer attachment before a blocker is added.");
            var traumas = SEntMan.System<TraumaSystem>();
            var trauma = traumas.AddTrauma(part,
                (part, SEntMan.GetComponent<WoundableComponent>(part)),
                (wound, SEntMan.GetComponent<TraumaInflicterComponent>(wound)),
                TraumaType.Dismemberment, FixedPoint2.New(5));
            Assert.That(AttachmentListed(), Is.False, "Opening an incision must not hide an active trauma blocker.");
            Assert.That(surgery.TryDoSurgeryStep(body, part, body, "SurgeryAttachLeftArm", "SurgeryStepInsertArmLeft", out var error), Is.False);
            Assert.That(error, Is.EqualTo(StepInvalidReason.SurgeryInvalid));

            traumas.RemoveTrauma((trauma, SEntMan.GetComponent<TraumaComponent>(trauma)));
            Assert.That(AttachmentListed(), Is.True, "Attachment should be offered again after the blocker is treated.");
        });
    }
}
