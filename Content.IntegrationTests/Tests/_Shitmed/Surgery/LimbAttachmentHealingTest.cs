using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Surgery;

[TestFixture]
public sealed class LimbAttachmentHealingTest : GameTest
{
    [TestCase(8, false)]
    [TestCase(2, false)]
    [TestCase(8, true)]
    public async Task SealingReattachedLimbHealsMatchingDamageExactlyOnce(int amountPerType, bool blockHeat)
    {
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);
        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.SpawnEntity("TendWoundsStepTestVictim", coords);
            SEntMan.GetComponent<SurgeryTargetComponent>(body).SepsisImmune = true;
            var torso = SEntMan.SpawnEntity("TendWoundsStepTestTorsoOrgan", coords);
            var arm = SEntMan.SpawnEntity("TendWoundsStepTestArmOrgan", coords);
            var containers = SEntMan.System<SharedContainerSystem>();
            containers.Insert(torso, containers.GetContainer(body, BodyComponent.ContainerID));
            var wounds = SEntMan.System<WoundSystem>();
            var damage = SEntMan.System<DamageableSystem>();
            var types = new[] { "Blunt", "Slash", "Heat" };
            foreach (var type in types)
                damage.TryChangeDamage(arm, new DamageSpecifier(SProtoMan.Index<DamageTypePrototype>(type), FixedPoint2.New(amountPerType)));
            if (blockHeat)
            {
                var wound = wounds.GetWoundableWounds(arm).Single(w => w.Comp.DamageType == "Heat");
                wound.Comp.CanBeHealed = false;
            }

            var surgery = SEntMan.System<SurgerySystem>();
            var procedure = surgery.GetSingleton("SurgeryAttachLeftArm")!.Value;
            var insert = surgery.GetSingleton("SurgeryStepInsertArmLeft")!.Value;
            var insertEvent = new SurgeryStepEvent(body, body, torso, arm, procedure, insert);
            SEntMan.EventBus.RaiseLocalEvent(insert, ref insertEvent);
            Assert.That(SEntMan.HasComponent<BodyPartReattachedComponent>(arm), Is.True);

            var seal = surgery.GetSingleton("SurgeryStepSealWounds")!.Value;
            var cautery = SEntMan.SpawnEntity("Cautery", coords);
            var ev = new SurgeryStepEvent(body, body, torso, cautery, procedure, seal);
            SEntMan.EventBus.RaiseLocalEvent(seal, ref ev);

            var expectedTotal = FixedPoint2.New(Math.Max(0, amountPerType * 3 - 12));
            Assert.That(wounds.GetWoundableSeverityPoint(arm), Is.EqualTo(expectedTotal));
            var totalDamage = FixedPoint2.Zero;
            foreach (var type in types)
            {
                var severity = wounds.GetWoundableWounds(arm)
                    .Where(w => !w.Comp.IsScar && w.Comp.DamageType == type)
                    .Aggregate(FixedPoint2.Zero, (sum, w) => sum + w.Comp.WoundSeverityPoint);
                var raw = wounds.GetTypeDamage(arm, type);
                Assert.That(raw, Is.EqualTo(severity), $"{type} damage must match its remaining wound severity.");
                totalDamage += raw;
            }
            Assert.That(totalDamage, Is.EqualTo(expectedTotal));
            if (blockHeat)
                Assert.That(wounds.GetTypeDamage(arm, "Heat"), Is.EqualTo(FixedPoint2.New(amountPerType)));
            Assert.That(SEntMan.HasComponent<BodyPartReattachedComponent>(arm), Is.False);
        });
    }
}
