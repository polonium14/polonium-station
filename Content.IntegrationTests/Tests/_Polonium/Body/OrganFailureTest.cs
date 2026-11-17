using Content.Server._Polonium.Medical.HealthAnalyzer;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Tests._Polonium.Medical;
using Content.Server._Polonium.Body;
using Content.Server.Body.Components;
using Content.Server.Medical.HealthAnalyzer;
using Content.Shared._Polonium.Body;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.CCVar;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Prototypes;
using Content.Shared.StatusEffectNew;
using Content.Shared.Traits.Assorted;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Polonium.Body;

/// <summary>
/// Sick organs fall into staged failure, kidneys set the pace of chemistry and thirst, dead organs poison,
/// and poison wears the kidneys and liver down.
/// </summary>
[TestFixture]
public sealed class OrganFailureTest : GameTest
{
    // CVars are changed, so the pair is not reused.
    public override PoolSettings PoolSettings => new() { Dirty = true };

    private static readonly ProtoId<SatiationTypePrototype> Thirst = "Thirst";
    private static readonly ProtoId<DamageTypePrototype> PoisonType = "Poison";

    private OrganHealingSystem Healing => SEntMan.System<OrganHealingSystem>();
    private OrganFailureSystem Failure => SEntMan.System<OrganFailureSystem>();

    private EntityUid Organ(EntityUid body, string category)
    {
        return SEntMan.GetComponent<BodyComponent>(body).Organs!.ContainedEntities
            .Single(o => SEntMan.GetComponent<OrganComponent>(o).Category == category);
    }

    private OrganIntegrityComponent Integrity(EntityUid organ)
    {
        return SEntMan.GetComponent<OrganIntegrityComponent>(organ);
    }

    private OrganFailureComponent FailureOf(EntityUid organ)
    {
        return SEntMan.GetComponent<OrganFailureComponent>(organ);
    }

    /// <summary>
    /// Leaves the organ at the given fraction of its integrity.
    /// </summary>
    private void Wear(EntityUid organ, float fraction)
    {
        var integrity = Integrity(organ);
        Healing.DamageOrgan((organ, integrity), integrity.IntegrityCap * (1f - fraction));
    }

    private static readonly string[] BothKidneys = ["KidneyLeft", "KidneyRight"];

    private FixedPoint2 Poison(EntityUid body)
    {
        var damage = SEntMan.System<DamageableSystem>().GetPositiveDamage((body, SEntMan.GetComponent<DamageableComponent>(body)));
        return damage.DamageDict.GetValueOrDefault(PoisonType);
    }

    private void SetChances(float contract, float stage, float toxin)
    {
        Server.CfgMan.SetCVar(CCVars.OrgansFailureContractChance, contract);
        Server.CfgMan.SetCVar(CCVars.OrgansFailureStageChance, stage);
        Server.CfgMan.SetCVar(CCVars.OrgansToxinOrganDamageChance, toxin);
    }

    [Test]
    public async Task SickOrganFailsAndRecovers()
    {
        SetChances(1f, 0f, 0f);
        var map = await Pair.CreateTestMap();
        var body = EntityUid.Invalid;
        var monkey = EntityUid.Invalid;

        await Server.WaitAssertion(() =>
        {
            body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            monkey = SEntMan.SpawnAtPosition("MobMonkey", map.GridCoords);
            Wear(Organ(body, "Liver"), 0.3f);
            Wear(Organ(monkey, "Liver"), 0.3f);
        });

        await Pair.RunSeconds(3);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(FailureOf(Organ(body, "Liver")).Stage, Is.EqualTo(1), "A sick liver falls into failure.");
                Assert.That(FailureOf(Organ(monkey, "Liver")).Stage, Is.Zero, "Animals are left alone.");
            });

            var liver = Organ(body, "Liver");
            Healing.HealOrgan((liver, Integrity(liver)), Integrity(liver).IntegrityCap);
        });

        await Pair.RunSeconds(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(FailureOf(Organ(body, "Liver")).Stage, Is.Zero, "A healed organ is no longer failing.");
        });
    }

    [Test]
    public async Task FailingKidneysPoisonAndWearThemselvesDown()
    {
        SetChances(0f, 0f, 1f);
        var map = await Pair.CreateTestMap();
        var body = EntityUid.Invalid;
        FixedPoint2 before = default;

        await Server.WaitAssertion(() =>
        {
            body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            foreach (var side in BothKidneys)
            {
                var kidney = Organ(body, side);
                Wear(kidney, 0.2f);
                Failure.SetStage((kidney, FailureOf(kidney)), 3);
                before = Integrity(kidney).OrganIntegrity;
            }
        });

        await Pair.RunSeconds(5);

        await Server.WaitAssertion(() =>
        {
            var kidneys = Organ(body, "KidneyLeft");
            Assert.Multiple(() =>
            {
                Assert.That(Poison(body), Is.InRange(FixedPoint2.New(1.5), FixedPoint2.New(3)),
                    "Stage 3 deals 0.5 poison a second, half from each kidney.");
                Assert.That(Integrity(kidneys).OrganIntegrity, Is.LessThan(before), "The failure wears the kidneys down.");
                Assert.That(Integrity(kidneys).OrganIntegrity, Is.GreaterThan(before - 2),
                    "The kidneys' own poison does not hurt the kidneys on top of that.");
            });
        });
    }

    [Test]
    public async Task PoisonWearsKidneysAndLiverDownOnce()
    {
        SetChances(0f, 0f, 1f);
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var kidneys = BothKidneys.Select(side => Integrity(Organ(body, side))).ToList();
            var liver = Integrity(Organ(body, "Liver"));
            var poison = new DamageSpecifier(Server.ProtoMan.Index(PoisonType), 10);
            FixedPoint2 Lost() => kidneys.Aggregate(FixedPoint2.Zero, (sum, k) => sum + k.IntegrityCap - k.OrganIntegrity);

            SEntMan.System<DamageableSystem>().ChangeDamage(body, poison, ignoreResistances: true);
            Assert.Multiple(() =>
            {
                Assert.That(Lost(), Is.EqualTo(FixedPoint2.New(1)), "A tenth of the poison.");
                Assert.That(kidneys.Count(k => k.OrganIntegrity < k.IntegrityCap), Is.EqualTo(1), "It hurts one kidney of the pair.");
                Assert.That(liver.OrganIntegrity, Is.EqualTo(liver.IntegrityCap - FixedPoint2.New(0.4)), "A twenty-fifth of the poison.");
            });

            SEntMan.System<CirculationSystem>().DealOrganDamage(body, poison);
            Assert.That(Lost(), Is.EqualTo(FixedPoint2.New(1)), "Poison from the organs themselves does not count.");
        });
    }

    [Test]
    public async Task DeadKidneysSlowChemistryAndThirstButNotBreathing()
    {
        SetChances(0f, 0f, 0f);
        var map = await Pair.CreateTestMap();
        var healthy = EntityUid.Invalid;
        var sick = EntityUid.Invalid;
        var thirst = new Dictionary<EntityUid, float>();

        await Server.WaitAssertion(() =>
        {
            PatientIntakeMachineTest.AddAir(SEntMan, map.MapUid);
            healthy = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            sick = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            foreach (var side in BothKidneys)
            {
                Assert.That(SEntMan.System<BodyDebugSystem>().TryKillOrgan(sick, Organ(sick, side)));
            }

            Assert.That(SEntMan.GetComponent<KidneyFunctionComponent>(sick).Rate, Is.EqualTo(0.5f));

            foreach (var body in new[] { healthy, sick })
            {
                Assert.That(SEntMan.System<BloodstreamSystem>().TryAddToBloodstream(body, new Solution("Dexalin", 10)));
            }

            // The same drink in both stomachs: dead kidneys digest it slower.
            var satiation = SEntMan.System<SatiationSystem>();
            foreach (var body in new[] { healthy, sick })
            {
                var comp = SEntMan.GetComponent<SatiationComponent>(body);
                satiation.ModifyValue((body, comp), Thirst, -100);
                thirst[body] = satiation.GetValueOrNull((body, comp), Thirst)!.Value;
                Assert.That(SEntMan.System<StomachSystem>().TryTransferSolution(Organ(body, "Stomach"), new Solution("Water", 30)));
            }
        });

        await Pair.RunSeconds(6);

        await Server.WaitAssertion(() =>
        {
            FixedPoint2 Left(EntityUid body)
            {
                var bloodstream = SEntMan.GetComponent<Content.Shared.Body.Components.BloodstreamComponent>(body);
                Entity<SolutionComponent>? blood = null;
                SEntMan.System<SharedSolutionContainerSystem>().ResolveSolution(body, bloodstream.BloodSolutionName, ref blood);
                return blood!.Value.Comp.Solution.GetTotalPrototypeQuantity("Dexalin");
            }

            float Quenched(EntityUid body)
            {
                return SEntMan.System<SatiationSystem>().GetValueOrNull((body, SEntMan.GetComponent<SatiationComponent>(body)), Thirst)!.Value
                       - thirst[body];
            }

            Assert.Multiple(() =>
            {
                Assert.That(Quenched(sick), Is.InRange(Quenched(healthy) * 0.3f, Quenched(healthy) * 0.7f),
                    "Dead kidneys quench thirst about half as fast.");
                Assert.That(Left(sick), Is.GreaterThan(Left(healthy)), "Dead kidneys make a drug last longer.");
                Assert.That(Poison(sick), Is.GreaterThan(FixedPoint2.New(3)), "Dead kidneys poison the body.");
                var respirator = SEntMan.GetComponent<RespiratorComponent>(sick);
                Assert.That(respirator.Saturation, Is.GreaterThan(respirator.SuffocationThreshold), "Breathing is not slowed.");
            });
        });
    }

    [Test]
    public async Task SickKidneysSlowDownUntilHealed()
    {
        SetChances(0f, 0f, 0f);
        var map = await Pair.CreateTestMap();
        var body = EntityUid.Invalid;

        await Server.WaitAssertion(() =>
        {
            body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            Assert.That(SEntMan.HasComponent<KidneyFunctionComponent>(body), Is.False, "Healthy kidneys work at full pace.");
            Wear(Organ(body, "KidneyLeft"), 0.3f);
        });

        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<KidneyFunctionComponent>(body).Rate, Is.EqualTo(0.75f));
            var kidneys = Organ(body, "KidneyLeft");
            Healing.HealOrgan((kidneys, Integrity(kidneys)), Integrity(kidneys).IntegrityCap);
        });

        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<KidneyFunctionComponent>(body), Is.False);
        });
    }

    [Test]
    public async Task StagesStopAtTheLastAndCanKillTheOrgan()
    {
        SetChances(0f, 1f, 0f);
        var map = await Pair.CreateTestMap();
        var body = EntityUid.Invalid;

        await Server.WaitAssertion(() =>
        {
            body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var liver = Organ(body, "Liver");
            Wear(liver, 0.3f);
            Failure.SetStage((liver, FailureOf(liver)), 1);

            var lungs = Organ(body, "LungLeft");
            Assert.That(SEntMan.System<BodyDebugSystem>().TrySetFailureStage(body, lungs, 1), Is.False,
                "A healthy organ cannot be put into failure.");

            var kidneys = Organ(body, "KidneyLeft");
            Wear(kidneys, 0.0005f);
            Failure.SetStage((kidneys, FailureOf(kidneys)), 3);
        });

        await Pair.RunSeconds(5);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(FailureOf(Organ(body, "Liver")).Stage, Is.EqualTo(3), "The failure climbs to the last stage and stays.");
                Assert.That(SEntMan.HasComponent<DeadOrganComponent>(Organ(body, "KidneyLeft")), "Failure can wear an organ down to death.");
            });
        });
    }

    [Test]
    public async Task OneDeadKidneySlowsButDoesNotPoison()
    {
        SetChances(0f, 0f, 0f);
        var map = await Pair.CreateTestMap();
        var body = EntityUid.Invalid;

        await Server.WaitAssertion(() =>
        {
            body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            Assert.That(SEntMan.System<BodyDebugSystem>().TryKillOrgan(body, Organ(body, "KidneyLeft")));
            Assert.That(SEntMan.GetComponent<KidneyFunctionComponent>(body).Rate, Is.EqualTo(0.75f), "The other kidney carries on, slower.");
        });

        await Pair.RunSeconds(3);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Poison(body), Is.EqualTo(FixedPoint2.Zero), "One working kidney keeps the body clean.");
        });
    }

    [Test]
    public async Task OneDeadLungLowersOxygen()
    {
        SetChances(0f, 0f, 0f);
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var human = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var diona = SEntMan.SpawnAtPosition("MobDiona", map.GridCoords);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<OxygenLimitComponent>(human), Is.False, "Two lungs give all the oxygen.");
                Assert.That(SEntMan.HasComponent<OxygenLimitComponent>(diona), Is.False, "A single unpaired lung is whole.");
            });

            Assert.That(SEntMan.System<BodyDebugSystem>().TryKillOrgan(human, Organ(human, "LungRight")));
            Assert.That(SEntMan.GetComponent<OxygenLimitComponent>(human).Cap, Is.EqualTo(0.6f), "One lung keeps 60% of the oxygen.");
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task NoWorkingLiverPoisons(bool remove)
    {
        SetChances(0f, 0f, 0f);
        var map = await Pair.CreateTestMap();
        var body = EntityUid.Invalid;

        await Server.WaitAssertion(() =>
        {
            body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var debug = SEntMan.System<BodyDebugSystem>();
            var liver = Organ(body, "Liver");
            Assert.That(remove ? debug.TryRemoveOrgan(body, liver) : debug.TryKillOrgan(body, liver));
        });

        await Pair.RunSeconds(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Poison(body), Is.InRange(FixedPoint2.New(4), FixedPoint2.New(6)), "One poison a second.");
        });
    }

    [Test]
    public async Task FailingHeartKnocksOutButLeavesRevivable()
    {
        SetChances(0f, 0f, 0f);
        var map = await Pair.CreateTestMap();
        var body = EntityUid.Invalid;

        await Server.WaitAssertion(() =>
        {
            PatientIntakeMachineTest.AddAir(SEntMan, map.MapUid);
            body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var heart = Organ(body, "Heart");
            Wear(heart, 0.2f);
            Failure.SetStage((heart, FailureOf(heart)), 3);
        });

        await Pair.RunSeconds(30);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.System<StatusEffectsSystem>().HasStatusEffect(body, CirculationSystem.Unconscious));
                Assert.That(SEntMan.System<MobStateSystem>().IsAlive(body));
                Assert.That(SEntMan.System<CirculationSystem>().IsStopped(body), Is.False, "The heart still beats.");
                Assert.That(SEntMan.HasComponent<UnrevivableComponent>(body), Is.False);
            });
        });
    }

    [Test]
    public async Task AnalyzerShowsStageAndSteadySpO2()
    {
        SetChances(0f, 0f, 0f);
        var map = await Pair.CreateTestMap();
        var body = EntityUid.Invalid;
        var readings = new List<float>();

        await Server.WaitAssertion(() =>
        {
            PatientIntakeMachineTest.AddAir(SEntMan, map.MapUid);
            body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var lungs = Organ(body, "LungLeft");
            Wear(lungs, 0.2f);
            Failure.SetStage((lungs, FailureOf(lungs)), 2);

            var scan = SEntMan.System<PatientScanSystem>().BuildScan(body, true);
            Assert.That(scan.Organs.Single(organ => organ.Category == "LungLeft").FailureStage, Is.EqualTo(2));
        });

        for (var i = 0; i < 10; i++)
        {
            await Pair.RunSeconds(1);
            await Server.WaitPost(() => readings.Add(SEntMan.System<CirculationSystem>().ReadSpO2(body)!.Value));
        }

        Assert.That(readings, Is.All.InRange(93f, 100f), "A body with stage 2 lungs still breathes well.");
    }

    /// <summary>
    /// The saturation swings with every breath; the reading must not follow it.
    /// </summary>
    [TestCase(false, 98.5f, 99f)]
    [TestCase(true, 95f, 97f)]
    public async Task SpO2StaysPutWhileBreathing(bool oneLung, float lowest, float highest)
    {
        SetChances(0f, 0f, 0f);
        var map = await Pair.CreateTestMap();
        var body = EntityUid.Invalid;
        var readings = new List<float>();

        await Server.WaitAssertion(() =>
        {
            PatientIntakeMachineTest.AddAir(SEntMan, map.MapUid);
            body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            if (oneLung)
                Assert.That(SEntMan.System<BodyDebugSystem>().TryKillOrgan(body, Organ(body, "LungLeft")));
        });

        // Long enough for the lost lung to show in a whole breath.
        await Pair.RunSeconds(20);
        for (var i = 0; i < 30; i++)
        {
            await Pair.RunSeconds(1);
            await Server.WaitPost(() => readings.Add(SEntMan.System<CirculationSystem>().ReadSpO2(body)!.Value));
        }

        Assert.Multiple(() =>
        {
            Assert.That(readings, Is.All.InRange(lowest, highest));
            Assert.That(readings.Max() - readings.Min(), Is.LessThan(0.5f), "The reading holds still over several breaths.");
        });
    }

    /// <summary>
    /// One lung runs short at the end of every breath, but the next breath comes in time: the patient stays awake.
    /// </summary>
    [Test]
    public async Task OneLungDoesNotKnockThePatientOut()
    {
        SetChances(0f, 0f, 0f);
        var map = await Pair.CreateTestMap();
        var body = EntityUid.Invalid;
        var passedOut = 0;

        await Server.WaitAssertion(() =>
        {
            PatientIntakeMachineTest.AddAir(SEntMan, map.MapUid);
            body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            Assert.That(SEntMan.System<BodyDebugSystem>().TryKillOrgan(body, Organ(body, "LungLeft")));
        });

        for (var i = 0; i < 60; i++)
        {
            await Pair.RunSeconds(1);
            await Server.WaitPost(() =>
            {
                if (SEntMan.System<StatusEffectsSystem>().HasStatusEffect(body, CirculationSystem.Unconscious))
                    passedOut++;
            });
        }

        Assert.That(passedOut, Is.Zero);
    }

    /// <summary>
    /// Smoking and a lost lung both read a little low, and a low reading brings a cough; a healthy body has neither.
    /// </summary>
    [Test]
    public async Task SmokingAndOneLungReadLowAndBringACough()
    {
        SetChances(0f, 0f, 0f);
        var map = await Pair.CreateTestMap();
        EntityUid healthy = default, smoker = default, oneLung = default;

        await Server.WaitAssertion(() =>
        {
            PatientIntakeMachineTest.AddAir(SEntMan, map.MapUid);
            healthy = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            smoker = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            oneLung = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            Assert.That(SEntMan.System<BloodstreamSystem>().TryAddToBloodstream(smoker, new Solution("Nicotine", 2)));
            Assert.That(SEntMan.System<BodyDebugSystem>().TryKillOrgan(oneLung, Organ(oneLung, "LungLeft")));
        });

        await Pair.RunSeconds(20);

        await Server.WaitAssertion(() =>
        {
            var circulation = SEntMan.System<CirculationSystem>();
            var symptoms = SEntMan.System<LowOxygenSymptomsSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(circulation.ReadSpO2(healthy), Is.InRange(98.5f, 99f));
                Assert.That(circulation.ReadSpO2(smoker), Is.InRange(95.5f, 96f), "Three points below a healthy reading.");
                Assert.That(symptoms.HasSymptoms(healthy, out _), Is.False);
                Assert.That(symptoms.HasSymptoms(smoker, out var smokerSevere) && !smokerSevere, "A smoker coughs.");
                Assert.That(symptoms.HasSymptoms(oneLung, out var lungSevere) && !lungSevere, "One lung coughs.");
            });
        });
    }

    /// <summary>
    /// Dexalin carries oxygen: the saturation holds without air for as long as the drug lasts.
    /// </summary>
    [Test]
    public async Task DexalinHoldsTheSaturationWithoutAir()
    {
        SetChances(0f, 0f, 0f);
        // No air on this map.
        var map = await Pair.CreateTestMap();
        EntityUid treated = default, untreated = default;

        await Server.WaitAssertion(() =>
        {
            treated = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            untreated = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            Assert.That(SEntMan.System<BloodstreamSystem>().TryAddToBloodstream(treated, new Solution("Dexalin", 15)));
        });

        await Pair.RunSeconds(15);

        await Server.WaitAssertion(() =>
        {
            var circulation = SEntMan.System<CirculationSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(circulation.ReadSpO2(treated), Is.GreaterThanOrEqualTo(95f));
                Assert.That(circulation.ReadSpO2(untreated), Is.LessThan(88f));
            });
        });
    }

    [Test]
    public async Task SpO2FallsWhenBreathingStops()
    {
        SetChances(0f, 0f, 0f);
        // No air on this map.
        var map = await Pair.CreateTestMap();
        var body = EntityUid.Invalid;
        await Server.WaitAssertion(() => body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords));
        await Pair.RunSeconds(12);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<CirculationSystem>().ReadSpO2(body), Is.LessThan(88f));
        });
    }
}
