using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._Polonium.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared.Body;
using Content.Shared.Hands.EntitySystems;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Polonium.Medical;

/// <summary>
/// One small item fits in the chest cavity, comes back out into the surgeon's hand, and drops out when the torso goes.
/// </summary>
[TestFixture]
public sealed class SurgicalCavityTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: CavityTestSmallItem
  components:
  - type: Item
    size: Small

- type: entity
  id: CavityTestLargeItem
  components:
  - type: Item
    size: Large
";

    private EntityUid Part(EntityUid body, string category)
    {
        return SEntMan.GetComponent<BodyComponent>(body).Organs!.ContainedEntities
            .Single(o => SEntMan.GetComponent<OrganComponent>(o).Category == category);
    }

    [Test]
    public async Task SmallItemGoesInAndComesOut()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var surgery = SEntMan.System<SharedSurgerySystem>();
            var cavity = SEntMan.System<SurgicalCavitySystem>();
            var hands = SEntMan.System<SharedHandsSystem>();

            var patient = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var surgeon = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var torso = Part(patient, "Torso");
            var insert = surgery.GetSingleton("SurgeryStepCavityInsert")!.Value;
            var remove = surgery.GetSingleton("SurgeryStepCavityRemove")!.Value;
            var insertSurgery = surgery.GetSingleton("SurgeryCavityInsert")!.Value;
            var removeSurgery = surgery.GetSingleton("SurgeryCavityRemove")!.Value;

            var large = SEntMan.SpawnAtPosition("CavityTestLargeItem", map.GridCoords);
            Assert.That(hands.TryPickupAnyHand(surgeon, large));
            Assert.That(surgery.CanPerformStepWithHeld(surgeon, patient, torso, insert, false, out _, insertSurgery), Is.False,
                "A large item does not fit.");
            Assert.That(hands.TryDrop(surgeon, large));

            Assert.That(surgery.CanPerformStepWithHeld(surgeon, patient, torso, insert, false, out _, insertSurgery), Is.False,
                "An empty hand has nothing to hide.");

            var small = SEntMan.SpawnAtPosition("CavityTestSmallItem", map.GridCoords);
            Assert.That(hands.TryPickupAnyHand(surgeon, small));
            Assert.That(surgery.CanPerformStepWithHeld(surgeon, patient, torso, insert, false, out _, insertSurgery));

            var insertEv = new SurgeryStepEvent(surgeon, patient, torso, small, insertSurgery, insert);
            SEntMan.EventBus.RaiseLocalEvent(insert, ref insertEv);
            Assert.Multiple(() =>
            {
                Assert.That(cavity.GetContents(torso), Is.EqualTo(small));
                Assert.That(surgery.IsStepComplete(patient, torso, "SurgeryStepCavityInsert", insertSurgery));
                Assert.That(surgery.IsStepComplete(patient, torso, "SurgeryStepCavityRemove", removeSurgery), Is.False);
            });

            var removeEv = new SurgeryStepEvent(surgeon, patient, torso, surgeon, removeSurgery, remove);
            SEntMan.EventBus.RaiseLocalEvent(remove, ref removeEv);
            Assert.Multiple(() =>
            {
                Assert.That(cavity.GetContents(torso), Is.Null);
                Assert.That(hands.IsHolding(surgeon, small), "The item comes out into the surgeon's hand.");
                Assert.That(surgery.IsStepComplete(patient, torso, "SurgeryStepCavityRemove", removeSurgery));
            });
        });
    }

    [Test]
    public async Task ItemDropsOutWhenTheTorsoIsGone()
    {
        var map = await Pair.CreateTestMap();
        EntityUid small = default, torso = default;
        await Server.WaitAssertion(() =>
        {
            var surgery = SEntMan.System<SharedSurgerySystem>();
            var patient = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var surgeon = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            torso = Part(patient, "Torso");
            small = SEntMan.SpawnAtPosition("CavityTestSmallItem", map.GridCoords);
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(surgeon, small));

            var insert = surgery.GetSingleton("SurgeryStepCavityInsert")!.Value;
            var ev = new SurgeryStepEvent(surgeon, patient, torso, small, surgery.GetSingleton("SurgeryCavityInsert")!.Value, insert);
            SEntMan.EventBus.RaiseLocalEvent(insert, ref ev);
            Assert.That(SEntMan.System<SurgicalCavitySystem>().GetContents(torso), Is.EqualTo(small));

            SEntMan.DeleteEntity(torso);
        });

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityExists(small), "The hidden item survives the torso.");
            Assert.That(SEntMan.System<SharedContainerSystem>().IsEntityInContainer(small), Is.False, "It lies on the floor.");
        });
    }
}
