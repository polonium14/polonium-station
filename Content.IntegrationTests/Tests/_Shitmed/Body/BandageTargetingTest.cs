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
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
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
public sealed class BandageTargetingTest : GameTest
{
    [TestCase(false, 5)]
    [TestCase(true, 0)]
    public async Task GauzeChecksTheSelectedLimbsBleeding(bool targetBleedingArm, int bodyBleeding)
    {
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);
        await Server.WaitAssertion(() =>
        {
            var patient = SEntMan.SpawnEntity("HealBurnGateTestPatient", coords);
            SEntMan.EnsureComponent<BloodstreamComponent>(patient);
            SEntMan.System<BloodstreamSystem>().TryModifyBleedAmount(patient, bodyBleeding);
            SEntMan.System<SharedHandsSystem>().AddHand(patient, "right", HandLocation.Right);
            var torso = SEntMan.SpawnEntity("TendWoundsStepTestTorsoOrgan", coords);
            var arm = SEntMan.SpawnEntity("TendWoundsStepTestArmOrgan", coords);
            var containers = SEntMan.System<SharedContainerSystem>();
            var organs = containers.GetContainer(patient, BodyComponent.ContainerID);
            containers.Insert(torso, organs);
            containers.Insert(arm, organs);
            var wound = SEntMan.SpawnEntity("HealedTraumaWound", coords);
            containers.Insert(wound, containers.GetContainer(arm, WoundableComponent.WoundContainerId));
            var wounds = SEntMan.System<WoundSystem>();
            wounds.SetWoundSeverity(wound, FixedPoint2.New(10));
            var bleed = SEntMan.EnsureComponent<BleedInflicterComponent>(wound);
            bleed.IsBleeding = true;
            bleed.Scaling = FixedPoint2.New(1);
            bleed.BleedingAmountRaw = FixedPoint2.New(1);
            wounds.RecomputeWoundableBleeds(arm);
            SEntMan.GetComponent<TargetingComponent>(patient).Target = targetBleedingArm ? TargetBodyPart.LeftArm : TargetBodyPart.Chest;
            var item = SEntMan.SpawnEntity("Gauze", coords);
            var ev = new AfterInteractEvent(patient, item, patient, SEntMan.GetComponent<TransformComponent>(patient).Coordinates, true);
            SEntMan.EventBus.RaiseLocalEvent(item, ev);
            Assert.That(SEntMan.GetComponent<DoAfterComponent>(patient).DoAfters.Values.Any(d => !d.Cancelled), Is.EqualTo(targetBleedingArm));
        });
    }
}
