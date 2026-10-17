using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Content.Shared.FixedPoint;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Shitmed.Surgery;

[TestFixture]
public sealed class SurgeryBleedingSideEffectsTest : GameTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task OpeningIncisionDoesNotClampExistingWounds(bool sepsisImmune)
    {
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);
        await Server.WaitAssertion(() =>
        {
            var patient = SEntMan.SpawnEntity("TendWoundsStepTestVictim", coords);
            SEntMan.GetComponent<SurgeryTargetComponent>(patient).SepsisImmune = sepsisImmune;
            var part = SEntMan.SpawnEntity("TendWoundsStepTestTorsoOrgan", coords);
            var containers = SEntMan.System<SharedContainerSystem>();
            containers.Insert(part, containers.GetContainer(patient, BodyComponent.ContainerID));
            var wound = SEntMan.SpawnEntity("HealedTraumaWound", coords);
            containers.Insert(wound, containers.GetContainer(part, WoundableComponent.WoundContainerId));
            var wounds = SEntMan.System<WoundSystem>();
            wounds.SetWoundSeverity(wound, FixedPoint2.New(10));
            var bleed = SEntMan.EnsureComponent<BleedInflicterComponent>(wound);
            bleed.Scaling = FixedPoint2.New(3);
            bleed.BleedingAmountRaw = FixedPoint2.New(2);
            bleed.IsBleeding = true;
            wounds.RecomputeWoundableBleeds(part);
            var surgery = SEntMan.System<SurgerySystem>();
            var step = surgery.GetSingleton("SurgeryStepOpenIncisionScalpel")!.Value;
            var tool = SEntMan.SpawnEntity("Scalpel", coords);
            var ev = new SurgeryStepEvent(patient, patient, part, tool,
                surgery.GetSingleton("SurgeryOpenIncision")!.Value, step);
            SEntMan.EventBus.RaiseLocalEvent(step, ref ev);
            Assert.That(bleed.IsBleeding, Is.True);
            Assert.That(bleed.Scaling, Is.EqualTo(FixedPoint2.New(3)));
            Assert.That(bleed.BleedingAmountRaw, Is.EqualTo(FixedPoint2.New(2)));
        });
    }
}
