using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Polonium.Body;
using Content.Shared._Polonium.Body;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared.Body;
using Content.Shared.FixedPoint;
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
