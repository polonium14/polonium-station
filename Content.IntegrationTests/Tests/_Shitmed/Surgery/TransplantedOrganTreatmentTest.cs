using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Conditions;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Content.Shared.FixedPoint;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Surgery;

[TestFixture]
public sealed class TransplantedOrganTreatmentTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: HealedTraumaHeart
  id: TransplantedTraumaLiver
  components:
  - type: Organ
    category: Liver
";

    [TestCase(false)]
    [TestCase(true)]
    public async Task TransplantKeepsDamageTreatableAfterDonorDeletion(bool healedDonorWound)
    {
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);
        EntityUid recipient = default, part = default, liver = default, trauma = default;
        await Server.WaitAssertion(() =>
        {
            var donor = SEntMan.SpawnEntity("HealedTraumaBody", coords);
            var donorPart = SEntMan.SpawnEntity("HealedTraumaTorso", coords);
            recipient = SEntMan.SpawnEntity("HealedTraumaBody", coords);
            part = SEntMan.SpawnEntity("HealedTraumaTorso", coords);
            liver = SEntMan.SpawnEntity("TransplantedTraumaLiver", coords);
            var wound = SEntMan.SpawnEntity("HealedTraumaWound", coords);
            SEntMan.GetComponent<SurgeryTargetComponent>(donor).SepsisImmune = true;
            SEntMan.GetComponent<SurgeryTargetComponent>(recipient).SepsisImmune = true;
            var containers = SEntMan.System<SharedContainerSystem>();
            var donorOrgans = containers.GetContainer(donor, BodyComponent.ContainerID);
            containers.Insert(donorPart, donorOrgans);
            containers.Insert(liver, donorOrgans);
            containers.Insert(part, containers.GetContainer(recipient, BodyComponent.ContainerID));
            containers.Insert(wound, containers.GetContainer(donorPart, WoundableComponent.WoundContainerId));
            var wounds = SEntMan.System<WoundSystem>();
            wounds.SetWoundSeverity(wound, FixedPoint2.New(10));
            var traumas = SEntMan.System<TraumaSystem>();
            trauma = traumas.AddTrauma(liver, (donorPart, SEntMan.GetComponent<WoundableComponent>(donorPart)),
                (wound, SEntMan.GetComponent<TraumaInflicterComponent>(wound)), TraumaType.OrganDamage, FixedPoint2.New(30));
            traumas.TryCreateOrganDamageModifier(liver, FixedPoint2.New(10), trauma, "First");
            traumas.TryCreateOrganDamageModifier(liver, FixedPoint2.New(20), trauma, "Second");
            if (healedDonorWound)
                wounds.SetWoundSeverity(wound, FixedPoint2.Zero);

            var surgery = SEntMan.System<SurgerySystem>();
            var remove = surgery.GetSingleton("SurgeryStepRemoveOrgan")!.Value;
            var ev = new SurgeryStepEvent(donor, donor, donorPart, donor,
                surgery.GetSingleton("SurgeryRemoveLiver")!.Value, remove);
            SEntMan.EventBus.RaiseLocalEvent(remove, ref ev);
            var insert = surgery.GetSingleton("SurgeryStepInsertLiver")!.Value;
            ev = new SurgeryStepEvent(recipient, recipient, part, liver,
                surgery.GetSingleton("SurgeryInsertLiver")!.Value, insert);
            SEntMan.EventBus.RaiseLocalEvent(insert, ref ev);

            Assert.That(SEntMan.GetComponent<TraumaComponent>(trauma).HoldingWoundable, Is.EqualTo(part));
            Assert.That(traumas.HasWoundableTrauma(donorPart, TraumaType.OrganDamage), Is.False);
            Assert.That(wounds.GetWoundableWounds(donorPart).Any(), Is.EqualTo(!healedDonorWound),
                "Keep the donor's flesh wound, but release an empty healed trauma scar.");
            Assert.That(wounds.GetWoundableSeverityPoint(part), Is.EqualTo(FixedPoint2.Zero));
            var scar = wounds.GetWoundableWounds(part).Single();
            Assert.That(scar.Comp.IsScar, Is.True);
            Assert.That(SEntMan.GetComponent<OrganIntegrityComponent>(liver).OrganIntegrity, Is.EqualTo(FixedPoint2.New(70)));
            SEntMan.DeleteEntity(donor);
        });

        await Pair.RunTicksSync(5);
        await Client.WaitAssertion(() =>
        {
            var clientBody = CEntMan.GetEntity(SEntMan.GetNetEntity(recipient));
            var clientPart = CEntMan.GetEntity(SEntMan.GetNetEntity(part));
            var surgery = CEntMan.System<Content.Client._Shitmed.Medical.Surgery.SurgerySystem>();
            var procedure = surgery.GetSingleton("SurgeryHealOrgans")!.Value;
            var valid = new SurgeryValidEvent(clientBody, clientPart, Category: "Torso");
            CEntMan.EventBus.RaiseLocalEvent(procedure, ref valid);
            Assert.That(valid.Cancelled, Is.False, "The recipient must offer organ repair even without another injury.");
            Assert.That(surgery.IsStepComplete(clientBody, clientPart, "SurgeryStepHealOrgans", procedure), Is.False);
        });

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(trauma), Is.False, "The donor no longer owns the transplanted organ's trauma.");
            var surgery = SEntMan.System<SurgerySystem>();
            var procedure = surgery.GetSingleton("SurgeryHealOrgans")!.Value;
            var step = surgery.GetSingleton("SurgeryStepHealOrgans")!.Value;
            var ev = new SurgeryStepEvent(recipient, recipient, part, recipient, procedure, step);
            SEntMan.EventBus.RaiseLocalEvent(step, ref ev);
            Assert.That(SEntMan.GetComponent<OrganIntegrityComponent>(liver).OrganIntegrity, Is.EqualTo(FixedPoint2.New(87)));
            SEntMan.EventBus.RaiseLocalEvent(step, ref ev);
            Assert.That(SEntMan.GetComponent<OrganIntegrityComponent>(liver).OrganIntegrity, Is.EqualTo(FixedPoint2.New(100)));
            Assert.That(surgery.IsStepComplete(recipient, part, "SurgeryStepHealOrgans", procedure), Is.True);
            Assert.That(SEntMan.System<WoundSystem>().GetWoundableWounds(part), Is.Empty);
        });
    }
}
