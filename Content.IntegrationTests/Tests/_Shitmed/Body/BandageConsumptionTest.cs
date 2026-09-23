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
public sealed class BandageConsumptionTest : GameTest
{
    [Test]
    public async Task StoppingBleedingConsumesGauzeEvenWithoutDamageToHeal()
    {
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);
        await Server.WaitAssertion(() =>
        {
            var patient = SEntMan.SpawnEntity("HealBurnGateTestPatient", coords);
            var torso = SEntMan.SpawnEntity("TendWoundsStepTestTorsoOrgan", coords);
            var containers = SEntMan.System<SharedContainerSystem>();
            containers.Insert(torso, containers.GetContainer(patient, BodyComponent.ContainerID));
            SEntMan.GetComponent<TargetingComponent>(patient).Target = TargetBodyPart.Chest;
            var wound = SEntMan.SpawnEntity("HealedTraumaWound", coords);
            containers.Insert(wound, containers.GetContainer(torso, WoundableComponent.WoundContainerId));
            var wounds = SEntMan.System<WoundSystem>();
            wounds.SetWoundSeverity(wound, FixedPoint2.New(10));
            var bleed = SEntMan.EnsureComponent<BleedInflicterComponent>(wound);
            bleed.IsBleeding = true;
            bleed.Scaling = FixedPoint2.New(1);
            bleed.BleedingAmountRaw = FixedPoint2.New(1);
            wounds.RecomputeWoundableBleeds(torso);
            var item = SEntMan.SpawnEntity("Gauze", coords);
            var stacks = SEntMan.System<SharedStackSystem>();
            var before = stacks.GetCount(item);
            var ev = new HealingDoAfterEvent();
            ev.DoAfter = new Content.Shared.DoAfter.DoAfter(0,
                new DoAfterArgs(SEntMan, patient, TimeSpan.Zero, ev, patient, patient, item), TimeSpan.Zero);
            SEntMan.EventBus.RaiseLocalEvent(patient, ev);
            Assert.That(bleed.IsBleeding, Is.False);
            Assert.That(stacks.GetCount(item), Is.EqualTo(before - 1), "Successful bleeding treatment must spend one gauze.");
            Assert.That(ev.Handled, Is.True);
        });
    }
}
