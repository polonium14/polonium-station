using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._Polonium.Body;
using Content.Server._Polonium.Medical.Dialysis;
using Content.Shared._Polonium.Medical.IV;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.FixedPoint;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Polonium.Medical;

/// <summary>
/// The dialysis machine strips non-medicine chemicals from the blood of the patient it is attached to and
/// keeps dead kidneys and liver from poisoning them.
/// </summary>
[TestFixture]
public sealed class DialysisTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> PoisonType = "Poison";
    private static readonly ProtoId<ReagentPrototype> Ethanol = "Ethanol";
    private static readonly ProtoId<ReagentPrototype> Bicaridine = "Bicaridine";

    private EntityUid Organ(EntityUid body, string category)
    {
        return SEntMan.GetComponent<BodyComponent>(body).Organs!.ContainedEntities
            .Single(o => SEntMan.GetComponent<OrganComponent>(o).Category == category);
    }

    private FixedPoint2 Poison(EntityUid body)
    {
        var damage = SEntMan.System<DamageableSystem>().GetPositiveDamage((body, SEntMan.GetComponent<DamageableComponent>(body)));
        return damage.DamageDict.GetValueOrDefault(PoisonType);
    }

    private FixedPoint2 InBlood(EntityUid body, string reagent)
    {
        var bloodstream = SEntMan.GetComponent<BloodstreamComponent>(body);
        Entity<SolutionComponent>? blood = null;
        SEntMan.System<SharedSolutionContainerSystem>().ResolveSolution(body, bloodstream.BloodSolutionName, ref blood);
        return blood!.Value.Comp.Solution.GetTotalPrototypeQuantity(reagent);
    }

    private void Attach(EntityUid machine, EntityUid? patient)
    {
        var iv = SEntMan.GetComponent<IVDripComponent>(machine);
        iv.AttachedTo = patient;
        SEntMan.Dirty(machine, iv);
    }

    /// <summary>
    /// A patient on the machine and an untouched one, both with the same chemicals in their blood.
    /// </summary>
    private async Task<(EntityUid Machine, EntityUid Patient, EntityUid Control)> Setup(bool emagged = false)
    {
        var map = await Pair.CreateTestMap();
        var result = default((EntityUid, EntityUid, EntityUid));

        await Server.WaitAssertion(() =>
        {
            PatientIntakeMachineTest.AddAir(SEntMan, map.MapUid);
            var machine = SEntMan.SpawnAtPosition("DialysisMachine", map.GridCoords);
            var patient = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);
            var control = SEntMan.SpawnAtPosition("MobHuman", map.GridCoords);

            if (emagged)
                SEntMan.EnsureComponent<EmaggedComponent>(machine).EmagType = EmagType.Interaction;

            foreach (var body in new[] { patient, control })
            {
                var solution = new Solution(Ethanol, 15);
                solution.AddReagent(Bicaridine, 15);
                Assert.That(SEntMan.System<BloodstreamSystem>().TryAddToBloodstream(body, solution));
            }

            Attach(machine, patient);
            result = (machine, patient, control);
        });

        return result;
    }

    [Test]
    public async Task FiltersEverythingButMedicine()
    {
        var (_, patient, control) = await Setup();
        await Pair.RunSeconds(4);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(InBlood(patient, "Ethanol"), Is.LessThanOrEqualTo(InBlood(control, "Ethanol") - 5), "About 2u a second filtered out.");
                Assert.That(InBlood(patient, "Bicaridine").Float(), Is.EqualTo(InBlood(control, "Bicaridine").Float()).Within(0.5f), "Medicine stays.");
                Assert.That(InBlood(patient, "Blood"), Is.GreaterThan(FixedPoint2.New(100)), "The patient's blood goes back in.");
            });
        });
    }

    [Test]
    public async Task EmaggedMachineStripsMedicine()
    {
        var (_, patient, control) = await Setup(emagged: true);
        await Pair.RunSeconds(4);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(InBlood(patient, "Bicaridine"), Is.LessThanOrEqualTo(InBlood(control, "Bicaridine") - 5));
                Assert.That(InBlood(patient, "Ethanol").Float(), Is.EqualTo(InBlood(control, "Ethanol").Float()).Within(0.5f));
            });
        });
    }

    [Test]
    public async Task ShieldsFromDeadKidneysAndPartlyFromDeadLiver()
    {
        var (machine, patient, control) = await Setup();

        await Server.WaitAssertion(() =>
        {
            var debug = SEntMan.System<BodyDebugSystem>();
            foreach (var body in new[] { patient, control })
            {
                Assert.That(debug.TryKillOrgan(body, Organ(body, "Kidneys")));
                Assert.That(debug.TryKillOrgan(body, Organ(body, "Liver")));
            }
        });

        await Pair.RunSeconds(6);

        FixedPoint2 onMachine = default;
        await Server.WaitAssertion(() =>
        {
            onMachine = Poison(patient);
            Assert.Multiple(() =>
            {
                // Dead kidneys and liver deal 2 a second without the machine, a third of 1 with it. The ethanol in
                // the blood adds a little poison of its own on both.
                Assert.That(Poison(control), Is.GreaterThan(FixedPoint2.New(9)));
                Assert.That(onMachine, Is.GreaterThan(FixedPoint2.New(1)), "The liver still poisons a little.");
                Assert.That(onMachine, Is.LessThan(Poison(control) / 2), "Far less than without the machine.");
            });

            Attach(machine, null);
        });

        await Pair.RunSeconds(3);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<OnDialysisComponent>(patient), Is.False);
            Assert.That(Poison(patient), Is.GreaterThan(onMachine + 4), "Off the machine the poisoning is back in full.");
        });
    }
}
