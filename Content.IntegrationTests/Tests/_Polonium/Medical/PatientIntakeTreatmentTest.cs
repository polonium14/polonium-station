// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Polonium.Medical.PatientIntake;
using Content.Server._Shitmed.Medical.Surgery;
using Content.Shared._Polonium.Medical.PatientIntake;
using Content.Shared._Shitmed.Body;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage.Systems;
using Content.Shared.Eye.Blinding.Systems;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Polonium.Medical;

/// <summary>
/// The other half of <see cref="PatientIntakeMachineTest.EveryIssueFailsTheCheck"/>: every issue the
/// generator can roll must be fixable with the treatment medbay actually has, or a binary pass is unwinnable.
/// Surgery is driven step by step the way the Shitmed tests do it; medicine is stood in for by its effect.
/// </summary>
[TestFixture]
[TestOf(typeof(PatientIntakeSystem))]
public sealed class PatientIntakeTreatmentTest : GameTest
{
    /// <summary>
    /// Category of a missing or swapped part -> the surgery and step that put one in.
    /// </summary>
    private static readonly Dictionary<string, (string Surgery, string Step)> InsertSurgery = new()
    {
        { "Eyes", ("SurgeryInsertEyes", "SurgeryStepInsertEyes") },
        { "Lungs", ("SurgeryInsertLungs", "SurgeryStepInsertLungs") },
        { "Liver", ("SurgeryInsertLiver", "SurgeryStepInsertLiver") },
        { "Stomach", ("SurgeryInsertStomach", "SurgeryStepInsertStomach") },
        { "HandLeft", ("SurgeryAttachLeftHand", "SurgeryStepInsertHandLeft") },
        { "HandRight", ("SurgeryAttachRightHand", "SurgeryStepInsertHandRight") },
        { "FootLeft", ("SurgeryAttachLeftFoot", "SurgeryStepInsertFootLeft") },
        { "FootRight", ("SurgeryAttachRightFoot", "SurgeryStepInsertFootRight") },
    };

    /// <summary>
    /// Hands and feet hang off a limb rather than sitting in the torso or head.
    /// </summary>
    private static readonly Dictionary<string, string> ParentLimb = new()
    {
        { "HandLeft", "ArmLeft" }, { "HandRight", "ArmRight" }, { "FootLeft", "LegLeft" }, { "FootRight", "LegRight" },
    };

    [Test]
    public async Task EveryIssueCanBeTreated()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var intake = entMan.System<PatientIntakeSystem>();
        var map = await pair.CreateTestMap();
        var coords = new MapCoordinates(Vector2.Zero, map.MapId);

        var issues = server.ProtoMan.EnumeratePrototypes<PatientIntakeIssuePrototype>().ToList();
        var patients = new Dictionary<string, EntityUid>();
        var donors = new Dictionary<string, EntityUid>();

        await server.WaitPost(() =>
        {
            PatientIntakeMachineTest.AddAir(entMan, map.MapUid);
            foreach (var issue in issues)
            {
                patients[issue.ID] = entMan.SpawnEntity("MobHuman", coords);
                donors[issue.ID] = entMan.SpawnEntity("MobHuman", coords);
            }
        });
        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            foreach (var issue in issues)
            {
                var patient = patients[issue.ID];
                entMan.GetComponent<SurgeryTargetComponent>(patient).SepsisImmune = true;
                var comp = entMan.AddComponent<PatientIntakePatientComponent>(patient);
                comp.ExpectedParts = intake.PartCategories(patient);
                Assert.That(intake.ApplyIssue(patient, comp, issue), Is.True, issue.ID);
            }
        });
        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            foreach (var issue in issues)
            {
                Assert.That(entMan.EntityExists(patients[issue.ID]), Is.True, $"{issue.ID} patient vanished");
                Treat(entMan, issue, patients[issue.ID], donors[issue.ID]);

                // Damage rolls its own traumas through the normal pipeline, like a real injury.
                RunStepUntilTreated(entMan, patients[issue.ID], TraumaType.BoneDamage, "SurgeryMendBones", "SurgeryStepMendBones");
                RunStepUntilTreated(entMan, patients[issue.ID], TraumaType.OrganDamage, "SurgeryHealOrgans", "SurgeryStepHealOrgans");
            }
        });
        await pair.RunTicksSync(5);
        await server.WaitPost(() =>
        {
            // Treatment can leave fresh surgical damage; medicine mops it up.
            foreach (var issue in issues)
            {
                Assert.That(entMan.EntityExists(patients[issue.ID]), Is.True, $"{issue.ID} patient vanished after treatment");
                HealEverything(entMan, patients[issue.ID]);
            }
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            foreach (var issue in issues)
            {
                var patient = patients[issue.ID];
                var failures = intake.GetFailures((patient, entMan.GetComponent<PatientIntakePatientComponent>(patient)));
                var diag = string.Join(" ; ", Organs(entMan, patient).Where(entMan.HasComponent<WoundableComponent>).Select(o =>
                {
                    var w = entMan.GetComponent<WoundableComponent>(o);
                    var ws = entMan.System<WoundSystem>().GetWoundableWounds(o).Select(x => $"{entMan.GetComponent<MetaDataComponent>(x).EntityPrototype?.ID}:{x.Comp.WoundSeverityPoint}:{x.Comp.IsScar}");
                    return $"{entMan.GetComponent<OrganComponent>(o).Category} {w.WoundableIntegrity}/{w.IntegrityCap} [{string.Join(",", ws)}]";
                }));
                Assert.That(failures, Is.Empty, $"{issue.ID} could not be treated: {string.Join(", ", failures)} DIAG {diag}");
            }
        });
    }

    private static void Treat(IEntityManager entMan, PatientIntakeIssuePrototype issue, EntityUid patient, EntityUid donor)
    {
        switch (issue.Kind)
        {
            case PatientIntakeIssueKind.Bone:
                RunStepUntilTreated(entMan, patient, TraumaType.BoneDamage, "SurgeryMendBones", "SurgeryStepMendBones");
                break;

            case PatientIntakeIssueKind.Organ:
                RunStepUntilTreated(entMan, patient, TraumaType.OrganDamage, "SurgeryHealOrgans", "SurgeryStepHealOrgans");
                break;

            case PatientIntakeIssueKind.Poison:
            {
                // Dialysis or purging chems: the reagent leaves the blood.
                var bloodstream = entMan.GetComponent<BloodstreamComponent>(patient);
                var solutions = entMan.System<SharedSolutionContainerSystem>();
                solutions.ResolveSolution(patient, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution);
                solutions.RemoveReagent(bloodstream.BloodSolution!.Value, issue.Reagent!.Value, issue.Quantity);
                break;
            }

            case PatientIntakeIssueKind.Blind:
                // Oculine.
                entMan.System<BlindableSystem>().AdjustEyeDamage(patient, -issue.EyeDamage);
                break;

            case PatientIntakeIssueKind.RemovePart:
            case PatientIntakeIssueKind.ReplacePart:
                foreach (var category in issue.Categories)
                    TransplantFromDonor(entMan, patient, donor, category, replace: issue.Kind == PatientIntakeIssueKind.ReplacePart);
                break;
        }
    }

    private static void RunStepUntilTreated(IEntityManager entMan, EntityUid patient, TraumaType trauma, string surgeryId, string stepId)
    {
        var surgery = entMan.System<SurgerySystem>();
        var traumas = entMan.System<TraumaSystem>();
        var procedure = surgery.GetSingleton(surgeryId)!.Value;
        var step = surgery.GetSingleton(stepId)!.Value;

        foreach (var part in Organs(entMan, patient).Where(entMan.HasComponent<WoundableComponent>))
        {
            for (var i = 0; i < 10 && traumas.HasWoundableTrauma(part, trauma); i++)
            {
                var ev = new SurgeryStepEvent(patient, patient, part, patient, procedure, step);
                entMan.EventBus.RaiseLocalEvent(step, ref ev);
            }
        }
    }

    /// <summary>
    /// Puts the donor's organ of this category in, removing the patient's own first for a swap request.
    /// </summary>
    private static void TransplantFromDonor(IEntityManager entMan, EntityUid patient, EntityUid donor, string category, bool replace)
    {
        var containers = entMan.System<SharedContainerSystem>();
        var patientBody = entMan.GetComponent<BodyComponent>(patient);
        if (!InsertSurgery.TryGetValue(category, out var insert))
            return;

        if (LimbTargetMap.TryGetOrganByCategory(entMan, patientBody, category, out var own))
        {
            if (!replace)
                return; // Not the part this issue removed.

            containers.Remove(own, patientBody.Organs!, force: true);
        }

        var donorBody = entMan.GetComponent<BodyComponent>(donor);
        Assert.That(LimbTargetMap.TryGetOrganByCategory(entMan, donorBody, category, out var organ), Is.True);
        containers.Remove(organ, donorBody.Organs!, force: true);

        var housing = ParentLimb.TryGetValue(category, out var limb)
            ? limb
            : (LimbTargetMap.GetVitalOrganCategories("Head").Contains(category) ? "Head" : "Torso");
        Assert.That(LimbTargetMap.TryGetOrganByCategory(entMan, patientBody, housing, out var part), Is.True);

        // Every step of the surgery, in order: the insert takes the organ as its tool.
        var surgery = entMan.System<SurgerySystem>();
        var procedure = surgery.GetSingleton(insert.Surgery)!.Value;
        foreach (var stepId in entMan.GetComponent<SurgeryComponent>(procedure).Steps)
        {
            var step = surgery.GetSingleton(stepId)!.Value;
            var ev = new SurgeryStepEvent(patient, patient, part, stepId == insert.Step ? organ : patient, procedure, step);
            entMan.EventBus.RaiseLocalEvent(step, ref ev);
        }
    }

    /// <summary>
    /// Brute/burn medicine, gauze and a blood pack: everything outside surgery a medic would use.
    /// </summary>
    private static void HealEverything(IEntityManager entMan, EntityUid patient)
    {
        var damageable = entMan.System<DamageableSystem>();
        var wounds = entMan.System<WoundSystem>();
        foreach (var organ in Organs(entMan, patient).Where(entMan.HasComponent<WoundableComponent>))
        {
            damageable.TryChangeDamage(organ, -damageable.GetPositiveDamage((organ, entMan.GetComponent<Content.Shared.Damage.Components.DamageableComponent>(organ))), ignoreResistances: true, interruptsDoAfters: false);
            wounds.TryHaltAllBleeding(organ, force: true);
        }

        var bloodstream = entMan.System<BloodstreamSystem>();
        bloodstream.TryModifyBleedAmount(patient, -100);
        bloodstream.TryModifyBloodLevel(patient, 1000);
    }

    private static List<EntityUid> Organs(IEntityManager entMan, EntityUid patient)
    {
        return entMan.System<SharedContainerSystem>().GetContainer(patient, BodyComponent.ContainerID).ContainedEntities.ToList();
    }
}
