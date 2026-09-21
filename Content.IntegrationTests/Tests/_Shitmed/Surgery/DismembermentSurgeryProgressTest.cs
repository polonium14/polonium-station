using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared._Shitmed.Medical.Surgery.Tools;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.FixedPoint;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Surgery;

[TestFixture]
public sealed class DismembermentSurgeryProgressTest : GameTest
{
    [Test]
    public async Task ANewDismembermentMustRequireFreshDeadSkinRemoval()
    {
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);
        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.SpawnEntity("TendWoundsStepTestVictim", coords);
            SEntMan.GetComponent<SurgeryTargetComponent>(body).SepsisImmune = true;
            var part = SEntMan.SpawnEntity("TendWoundsStepTestTorsoOrgan", coords);
            var containers = SEntMan.System<SharedContainerSystem>();
            containers.Insert(part, containers.GetContainer(body, BodyComponent.ContainerID));
            var wound = SEntMan.SpawnEntity("HealedTraumaWound", coords);
            containers.Insert(wound, containers.GetContainer(part, WoundableComponent.WoundContainerId));
            SEntMan.System<WoundSystem>().SetWoundSeverity(wound, FixedPoint2.New(10));
            var traumas = SEntMan.System<TraumaSystem>();
            void AddTrauma() => traumas.AddTrauma(part,
                (part, SEntMan.GetComponent<WoundableComponent>(part)),
                (wound, SEntMan.GetComponent<TraumaInflicterComponent>(wound)), TraumaType.Dismemberment, FixedPoint2.New(5));
            AddTrauma();
            var surgery = SEntMan.System<SurgerySystem>();
            var procedure = surgery.GetSingleton("SurgeryFixDismemberment")!.Value;
            var tool = SEntMan.SpawnEntity(null, coords);
            SEntMan.AddComponent<ScalpelComponent>(tool);
            SEntMan.AddComponent<BoneSawComponent>(tool);
            SEntMan.AddComponent<StitchesComponent>(tool);
            SEntMan.AddComponent<IncisionOpenComponent>(part);
            SEntMan.AddComponent<SkinRetractedComponent>(part);
            foreach (var stepId in new[] { "SurgeryStepRemoveSeveredSkin", "SurgeryStepRemoveLeftoverBones", "SurgeryStepSealDismembermentWound" })
            {
                var step = surgery.GetSingleton(stepId)!.Value;
                var ev = new SurgeryStepEvent(body, body, part, tool, procedure, step);
                SEntMan.EventBus.RaiseLocalEvent(step, ref ev);
            }
            Assert.That(traumas.HasWoundableTrauma(part, TraumaType.Dismemberment), Is.False);
            Assert.That(surgery.HasUnfinishedSurgerySteps(part), Is.False);
            AddTrauma();
            Assert.That(surgery.IsStepComplete(body, part, "SurgeryStepRemoveSeveredSkin", procedure), Is.False,
                "The first injury's marker must not complete dead-skin removal for the next injury.");
        });
    }

}
