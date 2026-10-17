using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Shitmed.Medical.Tourniquet;
using Content.Server._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Body;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Targeting;
using Content.Shared._Shitmed.Tourniquet;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Medical;
using Content.Shared.Medical.Healing;
using Content.Shared.Stacks;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

[TestFixture]
public sealed class OrganModifierLifetimeTest : GameTest
{
    [Test]
    public async Task RemovingOneModifierKeepsTheSameTraumasRemainingDamageTreatable()
    {
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);
        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.SpawnEntity("HealedTraumaBody", coords);
            var part = SEntMan.SpawnEntity("HealedTraumaTorso", coords);
            var heart = SEntMan.SpawnEntity("HealedTraumaHeart", coords);
            var wound = SEntMan.SpawnEntity("HealedTraumaWound", coords);
            var containers = SEntMan.System<SharedContainerSystem>();
            containers.Insert(part, containers.GetContainer(body, BodyComponent.ContainerID));
            containers.Insert(heart, containers.GetContainer(body, BodyComponent.ContainerID));
            containers.Insert(wound, containers.GetContainer(part, WoundableComponent.WoundContainerId));
            SEntMan.System<WoundSystem>().SetWoundSeverity(wound, FixedPoint2.New(10));
            var traumas = SEntMan.System<TraumaSystem>();
            var trauma = traumas.AddTrauma(heart, (part, SEntMan.GetComponent<WoundableComponent>(part)),
                (wound, SEntMan.GetComponent<TraumaInflicterComponent>(wound)), TraumaType.OrganDamage, FixedPoint2.New(30));
            traumas.TryCreateOrganDamageModifier(heart, FixedPoint2.New(10), trauma, "First");
            traumas.TryCreateOrganDamageModifier(heart, FixedPoint2.New(20), trauma, "Second");
            traumas.TryRemoveOrganDamageModifier(heart, trauma, "First");
            Assert.That(traumas.HasWoundableTrauma(part, TraumaType.OrganDamage), Is.True,
                "Removing one modifier must not discard the trauma that owns another modifier.");
            var surgery = SEntMan.System<SurgerySystem>();
            var step = surgery.GetSingleton("SurgeryStepHealOrgans")!.Value;
            var procedure = surgery.GetSingleton("SurgeryHealOrgans")!.Value;
            for (var i = 0; i < 2; i++)
            {
                var ev = new SurgeryStepEvent(body, body, part, body, procedure, step);
                SEntMan.EventBus.RaiseLocalEvent(step, ref ev);
            }
            Assert.That(SEntMan.GetComponent<OrganIntegrityComponent>(heart).OrganIntegrity, Is.EqualTo(FixedPoint2.New(100)));
            Assert.That(traumas.HasWoundableTrauma(part, TraumaType.OrganDamage), Is.False);
        });
    }
}
