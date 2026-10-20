using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Polonium.Medical.PatientIntake;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid.Prototypes;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Polonium.Medical;

[TestFixture]
[TestOf(typeof(PatientIntakeCheckSystem))]
public sealed class PatientIntakeCheckTest : GameTest
{
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";
    private static readonly ProtoId<ReagentPrototype> Bicaridine = "Bicaridine";
    private static readonly ProtoId<ReagentPrototype> Toxin = "Toxin";

    [Test]
    public async Task CleanRoundstartSpeciesPass()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var protoMan = server.ProtoMan;
        var check = entMan.System<PatientIntakeCheckSystem>();
        var map = await pair.CreateTestMap();

        var patients = new Dictionary<string, EntityUid>();
        await server.WaitPost(() =>
        {
            PatientIntakeMachineTest.AddAir(entMan, map.MapUid);
            foreach (var species in protoMan.EnumeratePrototypes<SpeciesPrototype>().Where(s => s.RoundStart))
                patients[species.ID] = entMan.SpawnEntity(species.Prototype, new MapCoordinates(Vector2.Zero, map.MapId));
        });

        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            foreach (var (species, patient) in patients)
            {
                var failures = check.Check(patient, PartCategories(entMan, patient));
                Assert.That(failures, Is.Empty, $"A freshly spawned {species} must pass: {string.Join(", ", failures)}");
            }
        });
    }

    [Test]
    public async Task InjuriesFail()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var check = entMan.System<PatientIntakeCheckSystem>();
        var map = await pair.CreateTestMap();

        EntityUid patient = default;
        HashSet<ProtoId<OrganCategoryPrototype>> parts = default!;
        await server.WaitPost(() =>
        {
            PatientIntakeMachineTest.AddAir(entMan, map.MapUid);
            patient = entMan.SpawnEntity("MobHuman", new MapCoordinates(Vector2.Zero, map.MapId));
        });
        await pair.RunTicksSync(5);
        await server.WaitPost(() => parts = PartCategories(entMan, patient));

        // Damage on one limb.
        await server.WaitPost(() =>
        {
            var arm = Organ(entMan, patient, "ArmLeft");
            var blunt = server.ProtoMan.Index(Blunt);
            entMan.System<DamageableSystem>().TryChangeDamage(arm, new DamageSpecifier(blunt, FixedPoint2.New(20)), ignoreResistances: true);
        });
        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            var kinds = check.Check(patient, parts).Select(f => f.Kind).ToList();
            Assert.That(kinds, Does.Contain(IntakeFailureKind.Damage));
            Assert.That(kinds, Does.Contain(IntakeFailureKind.LimbDamage));
        });

        // Poison in the blood.
        await server.WaitPost(() =>
        {
            var bloodstream = entMan.GetComponent<BloodstreamComponent>(patient);
            var solutions = entMan.System<SharedSolutionContainerSystem>();
            solutions.ResolveSolution(patient, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution);
            solutions.TryAddReagent(bloodstream.BloodSolution!.Value, "Toxin", 10);
        });
        await server.WaitAssertion(() =>
            Assert.That(check.Check(patient, parts).Select(f => f.Kind), Does.Contain(IntakeFailureKind.HarmfulReagent)));

        // Broken bone, damaged heart, bleeding and blood loss.
        await server.WaitPost(() =>
        {
            var trauma = entMan.System<TraumaSystem>();
            var leg = Organ(entMan, patient, "LegLeft");
            var bone = entMan.GetComponent<WoundableComponent>(leg).Bone!.ContainedEntities[0];
            trauma.ApplyDamageToBone(bone, 50);
            var heart = Organ(entMan, patient, "Heart");
            trauma.TryCreateOrganDamageModifier(heart, 10, patient, "PatientIntakeTest");
            var bloodstream = entMan.System<BloodstreamSystem>();
            bloodstream.TryModifyBleedAmount(patient, 5);
            bloodstream.TryModifyBloodLevel(patient, -150);
        });
        await server.WaitAssertion(() =>
        {
            var kinds = check.Check(patient, parts).Select(f => f.Kind).ToList();
            Assert.That(kinds, Does.Contain(IntakeFailureKind.Trauma));
            Assert.That(kinds, Does.Contain(IntakeFailureKind.OrganDamage));
            Assert.That(kinds, Does.Contain(IntakeFailureKind.Bleeding));
            Assert.That(kinds, Does.Contain(IntakeFailureKind.LowBlood));
        });

        // A limb removed from the body.
        await server.WaitPost(() =>
        {
            var containers = entMan.System<SharedContainerSystem>();
            var body = entMan.GetComponent<BodyComponent>(patient);
            containers.Remove(Organ(entMan, patient, "LegRight"), body.Organs!);
        });
        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
            Assert.That(check.Check(patient, parts), Does.Contain(new IntakeFailure(IntakeFailureKind.MissingPart, "LegRight"))));
    }

    [Test]
    public async Task MedicineIsNotHarmfulButPoisonIs()
    {
        var protoMan = Pair.Server.ProtoMan;
        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(PatientIntakeCheckSystem.IsHarmful(protoMan.Index(Bicaridine)), Is.False,
                "Overdose damage is conditional; a normal dose of medicine must pass.");
            Assert.That(PatientIntakeCheckSystem.IsHarmful(protoMan.Index(Toxin)), Is.True);
        });
    }

    private static HashSet<ProtoId<OrganCategoryPrototype>> PartCategories(IEntityManager entMan, EntityUid patient)
    {
        return entMan.GetComponent<BodyComponent>(patient).Organs!.ContainedEntities
            .Select(o => entMan.GetComponent<OrganComponent>(o).Category)
            .OfType<ProtoId<OrganCategoryPrototype>>()
            .ToHashSet();
    }

    private static EntityUid Organ(IEntityManager entMan, EntityUid patient, string category)
    {
        return entMan.GetComponent<BodyComponent>(patient).Organs!.ContainedEntities
            .First(o => entMan.GetComponent<OrganComponent>(o).Category == category);
    }
}
