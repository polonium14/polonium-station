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
public sealed class MissingLimbTopicalTest : GameTest
{
    [Test]
    public async Task TargetingMissingArmDoesNotStartHealingTorso()
    {
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);
        await Server.WaitAssertion(() =>
        {
            var patient = SEntMan.SpawnEntity("HealBurnGateTestPatient", coords);
            var torso = SEntMan.SpawnEntity("TendWoundsStepTestTorsoOrgan", coords);
            SEntMan.System<SharedHandsSystem>().AddHand(patient, "right", HandLocation.Right);
            var containers = SEntMan.System<SharedContainerSystem>();
            containers.Insert(torso, containers.GetContainer(patient, BodyComponent.ContainerID));
            SEntMan.System<DamageableSystem>().TryChangeDamage(torso,
                new DamageSpecifier(SProtoMan.Index<DamageTypePrototype>("Heat"), FixedPoint2.New(10)));
            SEntMan.GetComponent<TargetingComponent>(patient).Target = TargetBodyPart.LeftArm;
            var item = SEntMan.SpawnEntity("HealBurnGateTestItem", coords);
            var ev = new AfterInteractEvent(patient, item, patient, SEntMan.GetComponent<TransformComponent>(patient).Coordinates, true);
            SEntMan.EventBus.RaiseLocalEvent(item, ev);
            Assert.That(SEntMan.GetComponent<DoAfterComponent>(patient).DoAfters.Values.Any(d => !d.Cancelled), Is.False,
                "A missing limb must not fall back to treating unrelated body damage.");
        });
    }
    [Test]
    public async Task MissingPartAtCompletionDoesNotHealUnrelatedBodyDamage()
    {
        var map = await Pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);
        await Server.WaitAssertion(() =>
        {
            var patient = SEntMan.SpawnEntity("HealBurnGateTestPatient", coords);
            var torso = SEntMan.SpawnEntity("TendWoundsStepTestTorsoOrgan", coords);
            var containers = SEntMan.System<SharedContainerSystem>();
            containers.Insert(torso, containers.GetContainer(patient, BodyComponent.ContainerID));
            var damage = SEntMan.System<DamageableSystem>();
            damage.TryChangeDamage(torso, new DamageSpecifier(SProtoMan.Index<DamageTypePrototype>("Heat"), FixedPoint2.New(10)));
            SEntMan.GetComponent<TargetingComponent>(patient).Target = TargetBodyPart.LeftArm;
            var item = SEntMan.SpawnEntity("HealBurnGateTestItem", coords);
            var ev = new HealingDoAfterEvent();
            ev.DoAfter = new Content.Shared.DoAfter.DoAfter(0,
                new DoAfterArgs(SEntMan, patient, TimeSpan.Zero, ev, patient, patient, item), TimeSpan.Zero);
            SEntMan.EventBus.RaiseLocalEvent(patient, ev);
            Assert.That(damage.GetTotalDamage(patient), Is.EqualTo(FixedPoint2.New(10)));
            Assert.That(damage.GetTotalDamage(torso), Is.EqualTo(FixedPoint2.New(10)));
            Assert.That(ev.Handled, Is.False);
        });
    }

}
