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
public sealed class SurgeryStepDamageEffectsTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: SurgeryStepInsertOrgan
  id: SurgeryStepDamageTestInsert
  components:
  - type: SurgeryDamageChangeEffect
    damage:
      types:
        Heat: -6
";

    [TestCase("SurgeryStepSealTendWound", "Cautery", "Heat", 7.5)]
    [TestCase("SurgeryStepCloseIncision", "Cautery", "Heat", 7.5)]
    [TestCase("SurgeryStepClampInternalBleeders", "Hemostat", "Bloodloss", 7.5)]
    [TestCase("SurgeryStepOpenIncisionScalpel", "Scalpel", "Bloodloss", 15)]
    public async Task OrdinaryStepAppliesConfiguredDamageExactlyOnce(string stepId, string toolId, string damageType, double expected)
    {
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);
        await Server.WaitAssertion(() =>
        {
            var (body, part) = CreatePatient(coords, damageType);
            var tool = SEntMan.SpawnEntity(toolId, coords);
            var surgery = SEntMan.System<SurgerySystem>();
            var step = surgery.GetSingleton(stepId)!.Value;
            var ev = new SurgeryStepEvent(body, body, part, tool, surgery.GetSingleton("SurgeryTendWoundsBurn")!.Value, step);
            SEntMan.EventBus.RaiseLocalEvent(step, ref ev);
            Assert.That(SEntMan.System<WoundSystem>().GetTypeDamage(part, damageType), Is.EqualTo(FixedPoint2.New(expected)));
        });
    }

    [TestCase("success", 7)]
    [TestCase("wrong category", 10)]
    [TestCase("already inserted", 10)]
    [TestCase("detached target", 10)]
    public async Task OrganInsertionAppliesEffectOnlyAfterSuccessfulInsertion(string scenario, int expected)
    {
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);
        await Server.WaitAssertion(() =>
        {
            var (body, part) = CreatePatient(coords, "Heat");
            var tool = SEntMan.SpawnEntity(scenario == "wrong category" ? "HealedTraumaTorso" : "HealedTraumaHeart", coords);
            var containers = SEntMan.System<SharedContainerSystem>();
            var organs = containers.GetContainer(body, BodyComponent.ContainerID);
            if (scenario == "already inserted")
                containers.Insert(tool, organs);
            if (scenario == "detached target")
                containers.Remove(part, organs);

            var step = SEntMan.SpawnEntity("SurgeryStepDamageTestInsert", coords);
            var surgery = SEntMan.SpawnEntity("OrganReattachTestSurgery", coords);
            var ev = new SurgeryStepEvent(body, body, part, tool, surgery, step);
            SEntMan.EventBus.RaiseLocalEvent(step, ref ev);
            Assert.That(SEntMan.System<WoundSystem>().GetTypeDamage(part, "Heat"), Is.EqualTo(FixedPoint2.New(expected)));
            Assert.That(SEntMan.HasComponent<OrganReattachedComponent>(tool), Is.EqualTo(scenario == "success"));
        });
    }

    [TestCase(true, 7.5)]
    [TestCase(false, 10)]
    public async Task SealingOrganAppliesEffectOnlyWhenOrganIsPresent(bool present, double expected)
    {
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);
        await Server.WaitAssertion(() =>
        {
            var (body, part) = CreatePatient(coords, "Heat");
            var organ = SEntMan.SpawnEntity("HealedTraumaHeart", coords);
            SEntMan.EnsureComponent<OrganReattachedComponent>(organ);
            var containers = SEntMan.System<SharedContainerSystem>();
            if (present)
                containers.Insert(organ, containers.GetContainer(body, BodyComponent.ContainerID));

            var surgery = SEntMan.SpawnEntity("OrganReattachTestSurgery", coords);
            var step = SEntMan.System<SurgerySystem>().GetSingleton("SurgeryStepSealOrganWound")!.Value;
            var tool = SEntMan.SpawnEntity("Cautery", coords);
            var ev = new SurgeryStepEvent(body, body, part, tool, surgery, step);
            SEntMan.EventBus.RaiseLocalEvent(step, ref ev);
            Assert.That(SEntMan.System<WoundSystem>().GetTypeDamage(part, "Heat"), Is.EqualTo(FixedPoint2.New(expected)));
            Assert.That(SEntMan.HasComponent<OrganReattachedComponent>(organ), Is.EqualTo(!present));
        });
    }

    private (EntityUid Body, EntityUid Part) CreatePatient(MapCoordinates coords, string damageType)
    {
        var body = SEntMan.SpawnEntity("TendWoundsStepTestVictim", coords);
        var part = SEntMan.SpawnEntity("TendWoundsStepTestTorsoOrgan", coords);
        // Sanitation is tested separately; isolate the configured step effect here.
        SEntMan.GetComponent<SurgeryTargetComponent>(body).SepsisImmune = true;
        var containers = SEntMan.System<SharedContainerSystem>();
        containers.Insert(part, containers.GetContainer(body, BodyComponent.ContainerID));
        SEntMan.System<DamageableSystem>().TryChangeDamage(part,
            new DamageSpecifier(SProtoMan.Index<DamageTypePrototype>(damageType), FixedPoint2.New(10)));
        return (body, part);
    }
}
