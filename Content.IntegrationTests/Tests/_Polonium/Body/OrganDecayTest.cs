using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Polonium.Body;
using Content.Shared._Polonium.Body;
using Content.Shared._Polonium.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared.Body;
using Content.Shared.CCVar;
using Content.Shared.FixedPoint;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Polonium.Body;

/// <summary>
/// Organs outside a body die unless kept in a freezer, and surgery repairs a badly damaged organ once.
/// </summary>
[TestFixture]
public sealed class OrganDecayTest : GameTest
{
    // A CVar is changed, so the pair is not reused.
    public override PoolSettings PoolSettings => new() { Dirty = true };

    private EntityUid Organ(EntityUid body, string category)
    {
        return SEntMan.GetComponent<BodyComponent>(body).Organs!.ContainedEntities
            .Single(o => SEntMan.GetComponent<OrganComponent>(o).Category == category);
    }

    [Test]
    public async Task LooseOrganDiesUnlessFrozen()
    {
        // A whole organ lifetime in 30 seconds.
        Server.CfgMan.SetCVar(CCVars.OrgansDecayTime, 30f);
        var map = await Pair.CreateTestMap();
        EntityUid body = default, loose = default, frozen = default, brain = default, kept = default;

        await Server.WaitAssertion(() =>
        {
            var containers = SEntMan.System<SharedContainerSystem>();
            body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var organs = SEntMan.GetComponent<BodyComponent>(body).Organs!;
            loose = Organ(body, "Liver");
            frozen = Organ(body, "Spleen");
            brain = Organ(body, "Brain");
            kept = Organ(body, "Heart");
            // Taken out the way surgery does it: out of the body and cut loose from the torso.
            var relations = SEntMan.System<OrganRelationSystem>();
            foreach (var organ in new[] { loose, frozen, brain })
            {
                containers.Remove(organ, organs);
                relations.Orphan(organ);
            }

            var freezer = SEntMan.SpawnAtPosition("LockerFreezerBase", map.GridCoords);
            Assert.That(containers.Insert(frozen, containers.GetContainer(freezer, "entity_storage")));
        });

        await Pair.RunSeconds(15);

        await Server.WaitAssertion(() =>
        {
            var liver = SEntMan.GetComponent<OrganIntegrityComponent>(loose);
            Assert.That(liver.OrganIntegrity, Is.LessThan(liver.IntegrityCap), "An organ on the floor decays.");
        });

        await Pair.RunSeconds(30);

        await Server.WaitAssertion(() =>
        {
            bool Whole(EntityUid organ)
            {
                var integrity = SEntMan.GetComponent<OrganIntegrityComponent>(organ);
                return integrity.OrganIntegrity == integrity.IntegrityCap;
            }

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.Deleted(loose), "The dead organ replaces the decayed one.");
                Assert.That(SEntMan.EntityQuery<DeadOrganComponent>().Count(), Is.EqualTo(1));
                Assert.That(Whole(frozen), "A freezer stops the decay.");
                Assert.That(Whole(brain), "A brain may hold a player and does not decay.");
                Assert.That(Whole(kept), "An organ in a body does not decay.");
            });
        });
    }

    [Test]
    public async Task SurgeryRepairsAnOrganOnce()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var torso = Organ(body, "Torso");
            var liver = Organ(body, "Liver");
            var integrity = SEntMan.GetComponent<OrganIntegrityComponent>(liver);
            var healing = SEntMan.System<OrganHealingSystem>();
            var repair = SEntMan.System<OrganRepairSurgerySystem>();
            var surgery = SEntMan.System<Content.Server._Shitmed.Medical.Surgery.SurgerySystem>();
            var step = surgery.GetSingleton("SurgeryStepRepairOrgans")!.Value;

            Assert.That(repair.IsStepComplete(step, torso), "Healthy organs need no repair.");

            healing.DamageOrgan((liver, integrity), integrity.IntegrityCap * 0.9f);
            Assert.That(repair.IsStepComplete(step, torso), Is.False);

            var ev = new SurgeryStepEvent(body, body, torso, body, step, step);
            SEntMan.EventBus.RaiseLocalEvent(step, ref ev);
            Assert.Multiple(() =>
            {
                Assert.That(integrity.OrganIntegrity, Is.EqualTo(integrity.IntegrityCap * 0.6f));
                Assert.That(repair.IsStepComplete(step, torso));
            });

            healing.DamageOrgan((liver, integrity), integrity.IntegrityCap * 0.5f);
            Assert.That(repair.IsStepComplete(step, torso), "A repaired organ cannot be repaired again.");
        });
    }
}
