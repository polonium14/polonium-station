using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Polonium.Body;
using Content.Shared._Polonium.Body;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared.Body;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using NUnit.Framework;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Polonium.Body;

/// <summary>
/// Organ damage from hits: a battered part exposes its organs, an intact bone shields them,
/// and big organs are hit more often.
/// </summary>
[TestFixture]
public sealed class OrganTraumaTest : GameTest
{
    private const int Rolls = 2000;

    private EntityUid Organ(EntityUid body, string category)
    {
        return SEntMan.GetComponent<BodyComponent>(body).Organs!.ContainedEntities
            .Single(o => SEntMan.GetComponent<OrganComponent>(o).Category == category);
    }

    [TestCase("ClothingOuterHardsuitMedical", "WoundBlunt", 0f, TestName = "AcidSuitDoesNotStopAClub")]
    [TestCase("ClothingOuterHardsuitMedical", "WoundCaustic", 0.45f, TestName = "AcidSuitStopsAcid")]
    [TestCase("ClothingOuterArmorHeavy", "WoundBlunt", 0.4f, TestName = "HeavyArmorStopsAClub")]
    [TestCase("ClothingOuterArmorHeavy", "WoundSlash", 0.4f, TestName = "HeavyArmorStopsABlade")]
    public async Task ArmorOnlyCountsAgainstTheWoundsOwnDamage(string armor, string wound, float deduction)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var worn = SEntMan.SpawnAtPosition(armor, map.GridCoords);
            Assert.That(SEntMan.System<InventorySystem>().TryEquip(body, worn, "outerClothing", force: true));

            var inflicter = SEntMan.SpawnAtPosition(wound, map.GridCoords);
            var chance = SEntMan.System<TraumaSystem>().GetArmourChanceDeduction(
                body,
                (inflicter, SEntMan.GetComponent<TraumaInflicterComponent>(inflicter)),
                TraumaType.OrganDamage,
                "Torso");

            Assert.That(chance.Float(), Is.EqualTo(deduction).Within(0.01f));
        });
    }

    [Test]
    public async Task ArmorScalesTheChanceWithoutZeroingIt()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var traumas = SEntMan.System<TraumaSystem>();
            var inventory = SEntMan.System<InventorySystem>();

            float OrganChance(EntityUid body)
            {
                var torso = Organ(body, "Torso");
                var wound = SEntMan.SpawnAtPosition("WoundBlunt", map.GridCoords);
                var hits = 0;
                for (var i = 0; i < Rolls; i++)
                {
                    if (traumas.RandomOrganTraumaChance(
                            (torso, SEntMan.GetComponent<WoundableComponent>(torso)),
                            (wound, SEntMan.GetComponent<TraumaInflicterComponent>(wound))))
                        hits++;
                }

                return hits / (float) Rolls;
            }

            var bare = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var officer = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            Assert.That(inventory.TryEquip(officer, SEntMan.SpawnAtPosition("ClothingOuterArmorBasic", map.GridCoords), "outerClothing", force: true));
            Assert.That(inventory.TryEquip(officer, SEntMan.SpawnAtPosition("ClothingHeadHelmetBasic", map.GridCoords), "head", force: true));
            var heavy = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            Assert.That(inventory.TryEquip(heavy, SEntMan.SpawnAtPosition("ClothingOuterArmorHeavy", map.GridCoords), "outerClothing", force: true));

            var base17 = OrganChance(bare);
            Assert.Multiple(() =>
            {
                Assert.That(base17, Is.InRange(0.13f, 0.21f), "A blunt wound has a 17% organ chance on a bare body.");
                // Vest and helmet stack, and every worn piece counts for every body part.
                Assert.That(OrganChance(officer), Is.InRange(base17 * 0.6f, base17), "A vest and a helmet lower the chance a little.");
                Assert.That(OrganChance(heavy), Is.InRange(0.05f, base17 * 0.85f), "Heavy armor lowers it a lot and never to zero.");
            });
        });
    }

    [Test]
    public async Task DamagedPartRaisesOrganDamageChance()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var torso = Organ(body, "Torso");
            var woundable = SEntMan.GetComponent<WoundableComponent>(torso);

            FixedPoint2 Deduction(TraumaType type)
            {
                var ev = new TraumaChanceDeductionEvent(20, type, 0);
                SEntMan.EventBus.RaiseLocalEvent(torso, ref ev);
                return ev.ChanceDeduction;
            }

            Assert.That(Deduction(TraumaType.OrganDamage), Is.EqualTo(FixedPoint2.Zero), "An intact part adds nothing.");

            Assert.That(SEntMan.System<BodyDebugSystem>().TrySetLimbIntegrity(body, torso, woundable.IntegrityCap / 2));
            Assert.Multiple(() =>
            {
                Assert.That(Deduction(TraumaType.OrganDamage), Is.EqualTo(FixedPoint2.New(-OrganTraumaSystem.PartDamageChance / 2)));
                Assert.That(Deduction(TraumaType.BoneDamage), Is.EqualTo(FixedPoint2.Zero), "Only organ damage is affected.");
            });
        });
    }

    [Test]
    public async Task IntactBoneShieldsOrgans()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var torso = Organ(body, "Torso");
            var liver = Organ(body, "Liver");
            var bone = SEntMan.GetComponent<BoneComponent>(
                SEntMan.GetComponent<WoundableComponent>(torso).Bone!.ContainedEntities.Single());

            int Stopped()
            {
                var stopped = 0;
                for (var i = 0; i < Rolls; i++)
                {
                    var ev = new BeforeTraumaInducedEvent(20, liver, TraumaType.OrganDamage);
                    SEntMan.EventBus.RaiseLocalEvent(torso, ref ev);
                    if (ev.Cancelled)
                        stopped++;
                }

                return stopped;
            }

            Assert.That(Stopped() / (float) Rolls, Is.InRange(OrganTraumaSystem.BoneShield - 0.1f, OrganTraumaSystem.BoneShield + 0.1f));

            bone.BoneSeverity = BoneSeverity.Cracked;
            Assert.That(Stopped(), Is.Zero, "A cracked bone shields nothing.");
        });
    }

    [Test]
    public async Task OrganTakesAShareOfTheHit()
    {
        await Server.WaitAssertion(() =>
        {
            // A quarter by default: a hit of 20 costs an organ 5 integrity instead of 20.
            Assert.That(SEntMan.System<TraumaSystem>().OrganTraumaDamage(20), Is.EqualTo(FixedPoint2.New(5)));
        });
    }

    [Test]
    public async Task BigOrgansAreHitMoreOften()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var liver = Organ(body, "Liver");
            var heart = Organ(body, "Heart");
            EntityUid[] organs = [liver, heart];

            var system = SEntMan.System<OrganTraumaSystem>();
            var liverHits = 0;
            for (var i = 0; i < Rolls; i++)
            {
                if (system.PickOrgan(organs) == liver)
                    liverHits++;
            }

            // The liver weighs 3, the heart 1.
            Assert.That(liverHits / (float) Rolls, Is.InRange(0.65f, 0.85f));
        });
    }
}
