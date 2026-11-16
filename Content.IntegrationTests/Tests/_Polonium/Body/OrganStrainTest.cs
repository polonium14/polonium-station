using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Tests._Polonium.Medical;
using Content.Server._Polonium.Body;
using Content.Server.Body.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared.Atmos;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.CCVar;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Prototypes;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Polonium.Body;

/// <summary>
/// Everyday wear on the organs: drinking, smoking, stimulant overdoses, thirst, hot gas,
/// electric shocks and radiation.
/// </summary>
[TestFixture]
public sealed class OrganStrainTest : GameTest
{
    // CVars are changed, so the pair is not reused.
    public override PoolSettings PoolSettings => new() { Dirty = true };

    private static readonly ProtoId<SatiationTypePrototype> Thirst = "Thirst";
    private static readonly ProtoId<DamageTypePrototype> Poison = "Poison";
    private static readonly ProtoId<DamageTypePrototype> Shock = "Shock";
    private static readonly ProtoId<DamageTypePrototype> Radiation = "Radiation";

    private OrganIntegrityComponent Integrity(EntityUid body, string category)
    {
        var organ = SEntMan.GetComponent<BodyComponent>(body).Organs!.ContainedEntities
            .Single(o => SEntMan.GetComponent<OrganComponent>(o).Category == category);
        return SEntMan.GetComponent<OrganIntegrityComponent>(organ);
    }

    private FixedPoint2 Lost(EntityUid body, string category)
    {
        var integrity = Integrity(body, category);
        return integrity.IntegrityCap - integrity.OrganIntegrity;
    }

    private async Task<EntityUid> Human(string reagent = null, int amount = 0)
    {
        var map = await Pair.CreateTestMap();
        var body = EntityUid.Invalid;
        await Server.WaitAssertion(() =>
        {
            PatientIntakeMachineTest.AddAir(SEntMan, map.MapUid);
            body = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            if (reagent != null)
                Assert.That(SEntMan.System<BloodstreamSystem>().TryAddToBloodstream(body, new Solution(reagent, amount)));
        });

        return body;
    }

    [Test]
    public async Task HeavyDrinkingWearsTheLiverDown()
    {
        var drunk = await Human("Ethanol", 60);
        var sober = await Human("Ethanol", 5);
        await Pair.RunSeconds(60);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(Lost(drunk, "Liver"), Is.GreaterThan(FixedPoint2.Zero));
                Assert.That(Lost(sober, "Liver"), Is.EqualTo(FixedPoint2.Zero), "One drink stays under the threshold.");
            });
        });
    }

    [Test]
    public async Task NicotineWearsTheLungsDown()
    {
        var body = await Human("Nicotine", 5);
        await Pair.RunSeconds(30);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Lost(body, "LungLeft") + Lost(body, "LungRight"), Is.GreaterThan(FixedPoint2.Zero));
        });
    }

    [TestCase("Desoxyephedrine", 30, 10)]
    [TestCase("Ephedrine", 30, 10)]
    [TestCase("Stimulants", 60, 30)]
    public async Task StimulantOverdoseStrainsTheHeart(string reagent, int overdose, int dose)
    {
        var overdosed = await Human(reagent, overdose);
        var dosed = await Human(reagent, dose);
        await Pair.RunSeconds(4);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(Lost(overdosed, "Heart"), Is.GreaterThan(FixedPoint2.Zero));
                Assert.That(Lost(dosed, "Heart"), Is.EqualTo(FixedPoint2.Zero), "A normal dose is safe.");
            });
        });
    }

    [Test]
    public async Task ShockStrainsTheHeartAndRadiationTheSpleen()
    {
        Server.CfgMan.SetCVar(CCVars.OrgansToxinOrganDamageChance, 1f);
        var body = await Human();

        await Server.WaitAssertion(() =>
        {
            var damageable = SEntMan.System<DamageableSystem>();
            damageable.ChangeDamage(body, new DamageSpecifier(Server.ProtoMan.Index(Shock), 5), ignoreResistances: true);
            damageable.ChangeDamage(body, new DamageSpecifier(Server.ProtoMan.Index(Radiation), 5), ignoreResistances: true);

            Assert.Multiple(() =>
            {
                Assert.That(Lost(body, "Heart"), Is.EqualTo(FixedPoint2.New(1)), "A fifth of the shock.");
                Assert.That(Lost(body, "Spleen"), Is.EqualTo(FixedPoint2.New(0.5)), "A tenth of the radiation.");
                Assert.That(Lost(body, "Liver"), Is.EqualTo(FixedPoint2.Zero));
            });
        });
    }

    /// <summary>
    /// The liver's share of one poison tick is below what FixedPoint2 can hold; it must not round away.
    /// </summary>
    [Test]
    public async Task SmallPoisonTicksStillReachTheLiver()
    {
        Server.CfgMan.SetCVar(CCVars.OrgansToxinOrganDamageChance, 1f);
        var body = await Human();

        await Server.WaitAssertion(() =>
        {
            var damageable = SEntMan.System<DamageableSystem>();
            var tick = new DamageSpecifier(Server.ProtoMan.Index(Poison), 0.1);
            for (var i = 0; i < 200; i++)
            {
                damageable.ChangeDamage(body, tick, ignoreResistances: true);
            }

            // 0.004 a tick on average: 0.8 after 200 ticks.
            Assert.That(Lost(body, "Liver").Float(), Is.InRange(0.3f, 1.4f));
        });
    }

    [Test]
    public async Task ThirstStrainsTheKidneys()
    {
        var parched = await Human();
        var watered = await Human();

        await Server.WaitAssertion(() =>
        {
            var satiation = SEntMan.System<SatiationSystem>();
            satiation.SetValue((parched, SEntMan.GetComponent<SatiationComponent>(parched)), Thirst, 0f);
        });

        // The check runs once a minute.
        await Pair.RunSeconds(125);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(Lost(parched, "KidneyLeft"), Is.GreaterThan(FixedPoint2.Zero));
                Assert.That(Lost(parched, "KidneyRight"), Is.GreaterThan(FixedPoint2.Zero));
                Assert.That(Lost(watered, "KidneyLeft"), Is.EqualTo(FixedPoint2.Zero));
            });
        });
    }

    [Test]
    public async Task HotGasBurnsTheLungs()
    {
        var body = await Human();

        await Server.WaitAssertion(() =>
        {
            void Inhale(float temperature)
            {
                var gas = new GasMixture(1f) { Temperature = temperature };
                gas.AdjustMoles(Gas.Nitrogen, 0.1f);
                var ev = new InhaledGasEvent(gas);
                SEntMan.EventBus.RaiseLocalEvent(body, ref ev);
            }

            Inhale(Atmospherics.T20C);
            Assert.That(Lost(body, "LungLeft"), Is.EqualTo(FixedPoint2.Zero), "Room air does not burn.");

            Inhale(600f);
            Assert.Multiple(() =>
            {
                Assert.That(Lost(body, "LungLeft"), Is.EqualTo(FixedPoint2.New(0.5)));
                Assert.That(Lost(body, "LungRight"), Is.EqualTo(FixedPoint2.New(0.5)));
            });
        });
    }

    [TestCase(Gas.Plasma)]
    [TestCase(Gas.Tritium)]
    public async Task ToxicGasBurnsTheLungs(Gas toxic)
    {
        var body = await Human();

        await Server.WaitPost(() =>
        {
            // About one breath of the pure gas.
            var gas = new GasMixture(1f) { Temperature = Atmospherics.T20C };
            gas.AdjustMoles(toxic, 0.02f);
            var ev = new InhaledGasEvent(gas);
            SEntMan.EventBus.RaiseLocalEvent(body, ref ev);
        });
        await Pair.RunSeconds(2);

        await Server.WaitAssertion(() =>
        {
            // Tritium's radiation can also destroy a lung outright through an ordinary organ trauma,
            // and a dead lung has no integrity left to read.
            var lungs = SEntMan.GetComponent<BodyComponent>(body).Organs!.ContainedEntities
                .Where(o => SEntMan.GetComponent<OrganComponent>(o).Category is { } category && category.Id.StartsWith("Lung"))
                .ToList();
            Assert.That(lungs, Has.Count.EqualTo(2));
            Assert.That(lungs, Has.All.Matches<EntityUid>(lung =>
                !SEntMan.TryGetComponent<OrganIntegrityComponent>(lung, out var integrity)
                || integrity.OrganIntegrity < integrity.IntegrityCap));
        });
    }
}
