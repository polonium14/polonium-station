using System;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Polonium.Body;
using Content.Server._Polonium.Medical.Cpr;
using Content.Shared._Polonium.Body;
using Content.Shared._Shitmed.Body;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.CCVar;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage.Components;
using Robust.Shared.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffectNew;
using NUnit.Framework;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Polonium.Medical;

/// <summary>
/// Chest compressions keep the oxygen of a body that still has its heart, stop it rotting, can restart a failing heart,
/// break ribs through a real bone trauma and tire the rescuer.
/// </summary>
[TestFixture]
public sealed class CprTest : GameTest
{
    // CVars are changed, so the pair is not reused.
    public override PoolSettings PoolSettings => new() { Dirty = true };

    private static readonly ProtoId<DamageTypePrototype> Poison = "Poison";

    private CprSystem Cpr => SEntMan.System<CprSystem>();
    private BodyDebugSystem Debug => SEntMan.System<BodyDebugSystem>();

    private EntityUid Organ(EntityUid body, string category)
    {
        return SEntMan.GetComponent<BodyComponent>(body).Organs!.ContainedEntities
            .Single(o => SEntMan.GetComponent<OrganComponent>(o).Category == category);
    }

    private void SetChances(float ribs, float restart)
    {
        Server.CfgMan.SetCVar(CCVars.CprRibBreakChance, ribs);
        Server.CfgMan.SetCVar(CCVars.CprHeartRestartChance, restart);
        Server.CfgMan.SetCVar(CCVars.CprHeartRestartEpinephrineChance, restart);
        Server.CfgMan.SetCVar(CCVars.OrgansFailureContractChance, 0f);
        Server.CfgMan.SetCVar(CCVars.OrgansFailureStageChance, 0f);
    }

    private BoneComponent TorsoBone(EntityUid body)
    {
        var torso = Organ(body, "Torso");
        return SEntMan.GetComponent<BoneComponent>(SEntMan.GetComponent<WoundableComponent>(torso).Bone!.ContainedEntities[0]);
    }

    [Test]
    public async Task CompressionsHoldOxygenOfADeadHeart()
    {
        SetChances(0f, 0f);
        var map = await Pair.CreateTestMap();
        var patient = EntityUid.Invalid;
        var rescuer = EntityUid.Invalid;
        var ceiling = 0f;

        await Server.WaitAssertion(() =>
        {
            PatientIntakeMachineTest.AddAir(SEntMan, map.MapUid);
            patient = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            rescuer = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            Assert.That(Debug.TryKillOrgan(patient, Organ(patient, "Heart")));
        });

        // Let some oxygen go first, so holding it can be told apart from a full body.
        await Pair.RunSeconds(3);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Cpr.TryStart(rescuer, patient));
        });

        await Pair.RunSeconds(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Cpr.IsCompressed(patient), "The first cycle is done.");
            ceiling = SEntMan.GetComponent<OxygenLimitComponent>(patient).Ceiling;
        });

        await Pair.RunSeconds(8);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<OxygenLimitComponent>(patient).Ceiling, Is.EqualTo(ceiling),
                    "The oxygen stays while the compressions go on.");
                Assert.That(SEntMan.GetComponent<StaminaComponent>(rescuer).StaminaDamage, Is.GreaterThan(0f),
                    "Compressions tire the rescuer.");
            });
        });
    }

    [Test]
    public async Task CompressionsEndOncePatientIsOutOfCrit()
    {
        SetChances(0f, 0f);
        var map = await Pair.CreateTestMap();
        var patient = EntityUid.Invalid;

        // Out cold but alive with a beating heart: compressions can start, but there is nothing left to go on for.
        await Server.WaitAssertion(() =>
        {
            PatientIntakeMachineTest.AddAir(SEntMan, map.MapUid);
            patient = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var rescuer = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            Assert.That(SEntMan.System<StatusEffectsSystem>()
                .TrySetStatusEffectDuration(patient, CirculationSystem.Unconscious, TimeSpan.FromSeconds(60)));
            Assert.That(Cpr.TryStart(rescuer, patient));
        });

        // One compression takes half a second and holds the body for a second more.
        await Pair.RunSeconds(1);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Cpr.IsCompressed(patient), "The first cycle is done.");
        });

        await Pair.RunSeconds(10);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Cpr.IsCompressed(patient), Is.False, "No second cycle followed.");
        });
    }

    [Test]
    public async Task NoHeartNoCirculation()
    {
        SetChances(0f, 0f);
        var map = await Pair.CreateTestMap();
        var patient = EntityUid.Invalid;

        await Server.WaitAssertion(() =>
        {
            PatientIntakeMachineTest.AddAir(SEntMan, map.MapUid);
            patient = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var rescuer = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            Assert.That(Debug.TryRemoveOrgan(patient, Organ(patient, "Heart")));
            Assert.That(Cpr.TryStart(rescuer, patient), "Compressions can start on an empty chest.");
        });

        await Pair.RunSeconds(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Cpr.IsCompressed(patient), Is.False, "Without a heart no blood moves.");
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CompressionsBreatheForAPatientInCrit(bool compressions)
    {
        SetChances(0f, 0f);
        var map = await Pair.CreateTestMap();
        var patient = EntityUid.Invalid;

        await Server.WaitAssertion(() =>
        {
            PatientIntakeMachineTest.AddAir(SEntMan, map.MapUid);
            patient = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var rescuer = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            // Poison leaves no wound to bleed from, so the patient stays in crit for the whole test.
            var poison = Server.ResolveDependency<IPrototypeManager>().Index(Poison);
            SEntMan.System<DamageableSystem>().TryChangeDamage(patient, new DamageSpecifier(poison, FixedPoint2.New(120)), ignoreResistances: true);
            Assert.That(SEntMan.System<MobStateSystem>().IsCritical(patient), "Setup: 120 poison puts a human in crit.");
            if (compressions)
                Assert.That(Cpr.TryStart(rescuer, patient));
        });

        await Pair.RunSeconds(12);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<MobStateSystem>().IsCritical(patient), "Setup: the patient is still in crit.");
            var breathing = SEntMan.System<CirculationSystem>().ReadSpO2(patient) >= 88f;
            Assert.That(breathing, Is.EqualTo(compressions), "A patient in crit breathes only while someone gives compressions.");
        });
    }

    [Test]
    public async Task CompressionsStopTheDeadRotting()
    {
        SetChances(0f, 0f);
        var map = await Pair.CreateTestMap();
        var patient = EntityUid.Invalid;
        var rotting = SEntMan.System<SharedRottingSystem>();

        await Server.WaitAssertion(() =>
        {
            patient = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var rescuer = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            SEntMan.System<MobStateSystem>().ChangeMobState(patient, MobState.Dead);
            Assert.That(rotting.IsRotProgressing(patient, null), "A dead body rots.");
            Assert.That(Cpr.TryStart(rescuer, patient), "Compressions can be given to the dead.");
        });

        await Pair.RunSeconds(5);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(rotting.IsRotProgressing(patient, null), Is.False, "It does not rot while compressed.");
                Assert.That(SEntMan.System<MobStateSystem>().IsDead(patient), "Compressions bring no one back.");
            });
        });
    }

    [Test]
    public async Task CompressionsBreakRibsOnce()
    {
        SetChances(1f, 0f);
        var map = await Pair.CreateTestMap();
        var patient = EntityUid.Invalid;
        var rescuer = EntityUid.Invalid;

        await Server.WaitAssertion(() =>
        {
            PatientIntakeMachineTest.AddAir(SEntMan, map.MapUid);
            patient = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            rescuer = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            Assert.That(Debug.TryKillOrgan(patient, Organ(patient, "Heart")));
            Assert.That(Cpr.TryStart(rescuer, patient));
        });

        await Pair.RunSeconds(5);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(TorsoBone(patient).BoneSeverity, Is.EqualTo(BoneSeverity.Broken));
                Assert.That(SEntMan.System<TraumaSystem>().HasWoundableTrauma(Organ(patient, "Torso"), TraumaType.BoneDamage),
                    "A real trauma, so mend-bones treats it.");
            });
        });
    }

    [Test]
    public async Task CompressionsCanRestartAFailingHeart()
    {
        SetChances(0f, 1f);
        var map = await Pair.CreateTestMap();
        var patient = EntityUid.Invalid;

        await Server.WaitAssertion(() =>
        {
            PatientIntakeMachineTest.AddAir(SEntMan, map.MapUid);
            patient = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var rescuer = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var heart = Organ(patient, "Heart");
            Assert.That(Debug.TrySetOrganIntegrity(patient, heart, SEntMan.GetComponent<OrganIntegrityComponent>(heart).IntegrityCap * 0.2f));
            Assert.That(Debug.TrySetFailureStage(patient, heart, 3));
            Assert.That(SEntMan.System<BloodstreamSystem>().TryAddToBloodstream(patient, new Solution("Epinephrine", 5)));
            SEntMan.System<StatusEffectsSystem>().TryUpdateStatusEffectDuration(patient, CirculationSystem.Unconscious, TimeSpan.FromSeconds(30));
            Assert.That(Cpr.TryStart(rescuer, patient), "A patient out cold from a failing heart can be given compressions.");
        });

        await Pair.RunSeconds(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<OrganFailureComponent>(Organ(patient, "Heart")).Stage, Is.EqualTo(2));
        });
    }
}
