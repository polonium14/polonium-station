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
public sealed class OrganTreatmentAvailabilityTest : GameTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task RemovedOrganDoesNotKeepTreatmentAvailable(bool anotherDamagedOrgan)
    {
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);
        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.SpawnEntity("HealedTraumaBody", coords);
            var part = SEntMan.SpawnEntity("HealedTraumaTorso", coords);
            var containers = SEntMan.System<SharedContainerSystem>();
            var organs = containers.GetContainer(body, BodyComponent.ContainerID);
            containers.Insert(part, organs);
            var traumas = SEntMan.System<TraumaSystem>();
            EntityUid AddDamagedOrgan()
            {
                var organ = SEntMan.SpawnEntity("HealedTraumaHeart", coords);
                containers.Insert(organ, organs);
                var wound = SEntMan.SpawnEntity("HealedTraumaWound", coords);
                containers.Insert(wound, containers.GetContainer(part, WoundableComponent.WoundContainerId));
                SEntMan.System<WoundSystem>().SetWoundSeverity(wound, FixedPoint2.New(10));
                var trauma = traumas.AddTrauma(organ,
                    (part, SEntMan.GetComponent<WoundableComponent>(part)),
                    (wound, SEntMan.GetComponent<TraumaInflicterComponent>(wound)),
                    TraumaType.OrganDamage, FixedPoint2.New(10));
                traumas.TryCreateOrganDamageModifier(organ, FixedPoint2.New(10), trauma, "WoundableDamage");
                return organ;
            }

            var removed = AddDamagedOrgan();
            if (anotherDamagedOrgan)
                AddDamagedOrgan();
            var surgery = SEntMan.System<SurgerySystem>();
            var procedure = surgery.GetSingleton("SurgeryHealOrgans")!.Value;
            void AssertAvailable(bool expected)
            {
                var valid = new SurgeryValidEvent(body, part, Category: "Torso");
                SEntMan.EventBus.RaiseLocalEvent(procedure, ref valid);
                Assert.That(valid.Cancelled, Is.EqualTo(!expected));
                Assert.That(surgery.IsStepComplete(body, part, "SurgeryStepHealOrgans", procedure), Is.EqualTo(!expected));
            }

            AssertAvailable(true);
            containers.Remove(removed, organs);
            AssertAvailable(anotherDamagedOrgan);
            var step = surgery.GetSingleton("SurgeryStepHealOrgans")!.Value;
            var ev = new SurgeryStepEvent(body, body, part, body, procedure, step);
            SEntMan.EventBus.RaiseLocalEvent(step, ref ev);
            AssertAvailable(false);
            Assert.That(traumas.HasWoundableTrauma(part, TraumaType.OrganDamage), Is.True,
                "Removal must preserve the detached organ's trauma.");
            Assert.That(SEntMan.GetComponent<OrganIntegrityComponent>(removed).OrganIntegrity, Is.EqualTo(FixedPoint2.New(90)));

            containers.Insert(removed, organs);
            AssertAvailable(true);
            SEntMan.EventBus.RaiseLocalEvent(step, ref ev);
            AssertAvailable(false);
            Assert.That(SEntMan.GetComponent<OrganIntegrityComponent>(removed).OrganIntegrity, Is.EqualTo(FixedPoint2.New(100)));
        });
    }
}
