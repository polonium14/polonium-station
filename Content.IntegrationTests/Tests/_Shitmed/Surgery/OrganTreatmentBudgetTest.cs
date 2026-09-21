using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery;
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
public sealed class OrganTreatmentBudgetTest : GameTest
{
    [TestCase(30, 30)]
    [TestCase(10, 30)]
    [TestCase(17, 30)]
    [TestCase(5, 5)]
    public async Task TreatmentSharesBudgetAndOnlyHealsSelectedPartsTraumas(int firstDamage, int secondDamage)
    {
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);
        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.SpawnEntity("HealedTraumaBody", coords);
            var part = SEntMan.SpawnEntity("HealedTraumaTorso", coords);
            var otherPart = SEntMan.SpawnEntity("HealedTraumaTorso", coords);
            var first = SEntMan.SpawnEntity("HealedTraumaHeart", coords);
            var second = SEntMan.SpawnEntity("HealedTraumaHeart", coords);
            var unrelated = SEntMan.SpawnEntity("HealedTraumaHeart", coords);
            var containers = SEntMan.System<SharedContainerSystem>();
            var organs = containers.GetContainer(body, BodyComponent.ContainerID);
            foreach (var organ in new[] { part, otherPart, first, second, unrelated })
                containers.Insert(organ, organs);

            var traumas = SEntMan.System<TraumaSystem>();
            EntityUid AddTrauma(EntityUid woundable, EntityUid target, int amount)
            {
                var wound = SEntMan.SpawnEntity("HealedTraumaWound", coords);
                containers.Insert(wound, containers.GetContainer(woundable, WoundableComponent.WoundContainerId));
                SEntMan.System<WoundSystem>().SetWoundSeverity(wound, FixedPoint2.New(10));
                var trauma = traumas.AddTrauma(target,
                    (woundable, SEntMan.GetComponent<WoundableComponent>(woundable)),
                    (wound, SEntMan.GetComponent<TraumaInflicterComponent>(wound)),
                    TraumaType.OrganDamage, FixedPoint2.New(amount));
                traumas.TryCreateOrganDamageModifier(target, FixedPoint2.New(amount), trauma, "WoundableDamage");
                return trauma;
            }

            // Unrelated damage is inserted first to detect accidentally spending the budget on it.
            traumas.TryCreateOrganDamageModifier(first, FixedPoint2.New(5), first, "OtherDamage");
            var otherTrauma = AddTrauma(otherPart, first, 10);
            AddTrauma(otherPart, unrelated, 20);
            var firstTrauma = AddTrauma(part, first, firstDamage);
            var secondTrauma = AddTrauma(part, second, secondDamage);

            var surgery = SEntMan.System<SurgerySystem>();
            var step = surgery.GetSingleton("SurgeryStepHealOrgans")!.Value;
            var ev = new SurgeryStepEvent(body, body, part, body, surgery.GetSingleton("SurgeryHealOrgans")!.Value, step);
            SEntMan.EventBus.RaiseLocalEvent(step, ref ev);

            var firstIntegrity = SEntMan.GetComponent<OrganIntegrityComponent>(first);
            var secondIntegrity = SEntMan.GetComponent<OrganIntegrityComponent>(second);
            var expectedHealing = Math.Min(17, firstDamage + secondDamage);
            Assert.That(firstIntegrity.OrganIntegrity + secondIntegrity.OrganIntegrity,
                Is.EqualTo(FixedPoint2.New(185 - firstDamage - secondDamage + expectedHealing)));
            Assert.That(firstIntegrity.IntegrityModifiers[("OtherDamage", first)], Is.EqualTo(FixedPoint2.New(5)));
            Assert.That(firstIntegrity.IntegrityModifiers[("WoundableDamage", otherTrauma)], Is.EqualTo(FixedPoint2.New(10)));
            Assert.That(SEntMan.GetComponent<OrganIntegrityComponent>(unrelated).OrganIntegrity, Is.EqualTo(FixedPoint2.New(80)));

            if (firstDamage + secondDamage <= 17)
            {
                Assert.That(traumas.HasWoundableTrauma(part, TraumaType.OrganDamage), Is.False);
                Assert.That(firstIntegrity.IntegrityModifiers.ContainsKey(("WoundableDamage", firstTrauma)), Is.False);
                Assert.That(secondIntegrity.IntegrityModifiers.ContainsKey(("WoundableDamage", secondTrauma)), Is.False);
            }
            else if (firstDamage == 17)
            {
                Assert.That(firstIntegrity.IntegrityModifiers.ContainsKey(("WoundableDamage", firstTrauma)), Is.False,
                    "An exactly exhausted modifier must be removed even when unrelated damage remains.");
            }
        });
    }
}
