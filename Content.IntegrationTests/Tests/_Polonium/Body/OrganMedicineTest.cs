using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Polonium.Body;
using Content.Shared._Polonium.Body;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Server.Body.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.StatusEffectNew;
using Content.Shared.FixedPoint;
using NUnit.Framework;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Polonium.Body;

/// <summary>
/// Organ-healing reagents heal the organs they name and nothing else, and their overdose damages organs.
/// </summary>
[TestFixture]
public sealed class OrganMedicineTest : GameTest
{
    private OrganHealingSystem Healing => SEntMan.System<OrganHealingSystem>();

    private EntityUid Organ(EntityUid body, string category)
    {
        return SEntMan.GetComponent<BodyComponent>(body).Organs!.ContainedEntities
            .Single(o => SEntMan.GetComponent<OrganComponent>(o).Category == category);
    }

    private OrganIntegrityComponent Integrity(EntityUid body, string category)
    {
        return SEntMan.GetComponent<OrganIntegrityComponent>(Organ(body, category));
    }

    /// <summary>
    /// Spawns a human with the listed organs damaged by 10 and the reagent in its blood.
    /// </summary>
    private async Task<EntityUid> Patient(string reagent, int amount, params string[] damaged)
    {
        var map = await Pair.CreateTestMap();
        var body = EntityUid.Invalid;
        await Server.WaitAssertion(() =>
        {
            body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            foreach (var category in damaged)
            {
                var organ = Organ(body, category);
                Healing.DamageOrgan((organ, SEntMan.GetComponent<OrganIntegrityComponent>(organ)), 10);
            }

            if (amount > 0)
                Assert.That(SEntMan.System<BloodstreamSystem>().TryAddToBloodstream(body, new Solution(reagent, amount)));
        });

        return body;
    }

    [Test]
    public async Task PenteticAcidHealsItsOrgansOnly()
    {
        var body = await Patient("PenteticAcid", 10, "KidneyLeft", "Liver", "Heart");
        await Pair.RunSeconds(4);

        await Server.WaitAssertion(() =>
        {
            var kidneys = Integrity(body, "KidneyLeft");
            var heart = Integrity(body, "Heart");
            Assert.Multiple(() =>
            {
                // 1.5 per second; the first tick may land either side of the window.
                Assert.That(kidneys.OrganIntegrity, Is.InRange(kidneys.IntegrityCap - 10 + 3, kidneys.IntegrityCap - 10 + 7.5));
                Assert.That(Integrity(body, "Liver").OrganIntegrity, Is.GreaterThan(Integrity(body, "Liver").IntegrityCap - 10));
                Assert.That(heart.OrganIntegrity, Is.EqualTo(heart.IntegrityCap - 10), "The heart is not on the list.");
            });
        });
    }

    [TestCase("Oculine", "Eyes")]
    [TestCase("Mannitol", "Brain")]
    [TestCase("Salbutamol", "Spleen")]
    [TestCase("Perfluorodecalin", "Spleen")]
    public async Task MedicineHealsItsOrgan(string reagent, string category)
    {
        var map = await Pair.CreateTestMap();
        var body = EntityUid.Invalid;
        FixedPoint2 damaged = default;
        await Server.WaitAssertion(() =>
        {
            body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var organ = Organ(body, category);
            var integrity = SEntMan.GetComponent<OrganIntegrityComponent>(organ);
            // Half the organ's cap: clearly damaged, far from destroying it.
            Healing.DamageOrgan((organ, integrity), integrity.IntegrityCap / 2);
            damaged = integrity.OrganIntegrity;
            Assert.That(SEntMan.System<BloodstreamSystem>().TryAddToBloodstream(body, new Solution(reagent, 10)));
        });

        await Pair.RunSeconds(4);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Integrity(body, category).OrganIntegrity, Is.GreaterThan(damaged));
        });
    }

    [Test]
    public async Task PenteticAcidFlushesOtherChemicals()
    {
        var body = await Patient("PenteticAcid", 10);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.System<BloodstreamSystem>().TryAddToBloodstream(body, new Solution("Bicaridine", 10))));

        await Pair.RunSeconds(4);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(InBlood(body, "Bicaridine"), Is.EqualTo(FixedPoint2.Zero), "Other drugs are flushed out.");
                Assert.That(InBlood(body, "PenteticAcid"), Is.GreaterThan(FixedPoint2.Zero), "The acid itself stays.");
            });
        });
    }

    [Test]
    public async Task PerfluorodecalinBreathesForTheBodyInVacuum()
    {
        // The test map has no air, so an untreated body would start to suffocate at once.
        var body = await Patient("Perfluorodecalin", 15);
        await Pair.RunSeconds(10);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.System<StatusEffectsSystem>().HasEffectComp<LiquidBreathingStatusEffectComponent>(body));
                Assert.That(SEntMan.System<RespiratorSystem>().IsBreathing(body), "The reagent keeps the blood oxygenated.");
            });
        });
    }

    private FixedPoint2 InBlood(EntityUid body, string reagent)
    {
        var bloodstream = SEntMan.GetComponent<BloodstreamComponent>(body);
        Entity<SolutionComponent>? blood = null;
        SEntMan.System<SharedSolutionContainerSystem>().ResolveSolution(body, bloodstream.BloodSolutionName, ref blood);
        return blood!.Value.Comp.Solution.GetTotalPrototypeQuantity(reagent);
    }

    [Test]
    public async Task PenteticAcidOverdoseDamagesKidneys()
    {
        var body = await Patient("PenteticAcid", 40);
        await Pair.RunSeconds(4);

        await Server.WaitAssertion(() =>
        {
            var kidneys = Integrity(body, "KidneyLeft");
            Assert.That(kidneys.OrganIntegrity, Is.LessThan(kidneys.IntegrityCap), "Above 15u the kidneys lose more than they gain.");
            Assert.That(kidneys.IntegrityModifiers.Keys.Select(key => key.Item1), Does.Contain(OrganHealingSystem.ChemicalDamageId));
        });
    }

    [Test]
    public async Task OverdoseCanKillAnOrgan()
    {
        var map = await Pair.CreateTestMap();
        var body = EntityUid.Invalid;
        await Server.WaitAssertion(() =>
        {
            body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var kidneys = Organ(body, "KidneyLeft");
            var integrity = SEntMan.GetComponent<OrganIntegrityComponent>(kidneys);
            Healing.DamageOrgan((kidneys, integrity), integrity.IntegrityCap - 2);
            Assert.That(SEntMan.System<BloodstreamSystem>().TryAddToBloodstream(body, new Solution("PenteticAcid", 40)));
        });

        // The kidneys die inside the metabolism update that damages them.
        await Pair.RunSeconds(6);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<DeadOrganComponent>(Organ(body, "KidneyLeft")));
        });
    }

    [Test]
    public async Task PerfluorodecalinLeavesDeadLungsAlone()
    {
        var map = await Pair.CreateTestMap();
        var body = EntityUid.Invalid;
        await Server.WaitAssertion(() =>
        {
            body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            Assert.That(SEntMan.System<BodyDebugSystem>().TryKillOrgan(body, Organ(body, "LungLeft")));
            Assert.That(SEntMan.System<BloodstreamSystem>().TryAddToBloodstream(body, new Solution("Perfluorodecalin", 30)));
        });

        await Pair.RunSeconds(3);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<DeadOrganComponent>(Organ(body, "LungLeft")), "A dead organ is past healing.");
        });
    }

    [Test]
    public async Task CryoxadoneNeedsCold()
    {
        var body = await Patient("Cryoxadone", 10, "Heart");
        await Pair.RunSeconds(3);

        await Server.WaitAssertion(() =>
        {
            var heart = Integrity(body, "Heart");
            Assert.That(heart.OrganIntegrity, Is.EqualTo(heart.IntegrityCap - 10), "A warm body is not healed.");
        });
    }

    [Test]
    public async Task BloodstreamDrugsNeedAWorkingHeart()
    {
        var body = await Patient("PenteticAcid", 0, "KidneyLeft");
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<BodyDebugSystem>().TryKillOrgan(body, Organ(body, "Heart")));
            Assert.That(SEntMan.System<BloodstreamSystem>().TryAddToBloodstream(body, new Solution("PenteticAcid", 10)));
        });

        await Pair.RunSeconds(3);

        await Server.WaitAssertion(() =>
        {
            var kidneys = Integrity(body, "KidneyLeft");
            Assert.That(kidneys.OrganIntegrity, Is.EqualTo(kidneys.IntegrityCap - 10), "Without a heart the blood chemistry stands still.");
        });
    }

    [Test]
    public async Task ChemicalDamageIsInvisibleToSurgery()
    {
        var body = await Patient("PenteticAcid", 0, "Liver");
        await Server.WaitAssertion(() =>
        {
            var traumas = SEntMan.System<Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems.TraumaSystem>();
            Assert.That(traumas.HasBodyTrauma(body, Content.Shared._Shitmed.Medical.Surgery.Traumas.TraumaType.OrganDamage), Is.False);
            Assert.That(Integrity(body, "Liver").OrganIntegrity, Is.LessThan(Integrity(body, "Liver").IntegrityCap));
        });
    }
}
