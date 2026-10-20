using System.Linq;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared.Body;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects.Effects.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Implants.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Medical.PatientIntake;

public enum IntakeFailureKind : byte
{
    Dead,
    Damage,
    LimbDamage,
    OrganDamage,
    Trauma,
    Bleeding,
    MissingPart,
    UnfinishedSurgery,
    LowBlood,
    HarmfulReagent,
    Blind,
    Implant,
}

/// <param name="Subject">Organ/part/reagent name for the receipt, if the failure is about one.</param>
public readonly record struct IntakeFailure(IntakeFailureKind Kind, string? Subject = null);

/// <summary>
/// The "whole body healthy" judgment the patient intake machine runs on return. Also used by
/// the health analyzer readout, so medics see exactly what the machine will fail them on.
/// </summary>
public sealed class PatientIntakeCheckSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private TraumaSystem _trauma = default!;
    [Dependency] private SharedSurgerySystem _surgery = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    /// <summary>
    /// Damage at or below this counts as healed - healing items leave fractional remainders.
    /// </summary>
    public static readonly FixedPoint2 DamageTolerance = FixedPoint2.New(1);

    /// <summary>
    /// Minimum blood level fraction for a pass.
    /// </summary>
    public const float MinBloodLevel = 0.9f;

    /// <param name="expectedParts">
    /// Woundable part categories the body had when the case started. Limbs aren't in
    /// <see cref="BodyComponent.ExpectedOrgans"/>, so a missing limb is only caught by this.
    /// </param>
    public List<IntakeFailure> Check(EntityUid patient, IReadOnlyCollection<ProtoId<OrganCategoryPrototype>>? expectedParts = null)
    {
        var failures = new List<IntakeFailure>();

        if (_mobState.IsDead(patient))
            failures.Add(new(IntakeFailureKind.Dead));

        if (TryComp<DamageableComponent>(patient, out var damageable)
            && _damageable.GetPositiveDamage((patient, damageable)).GetTotal() > DamageTolerance)
            failures.Add(new(IntakeFailureKind.Damage));

        if (TryComp<BodyComponent>(patient, out var body))
            CheckOrgans(body, expectedParts, failures);

        if (TryComp<BloodstreamComponent>(patient, out var bloodstream))
        {
            if (bloodstream.BleedAmount > 0)
                failures.Add(new(IntakeFailureKind.Bleeding));

            if (_bloodstream.GetBloodLevel((patient, bloodstream)) < MinBloodLevel)
                failures.Add(new(IntakeFailureKind.LowBlood));

            CheckReagents(patient, bloodstream, failures);
        }

        if (HasComp<PermanentBlindnessComponent>(patient)
            || TryComp<BlindableComponent>(patient, out var blindable) && (blindable.IsBlind || blindable.EyeDamage > 0))
            failures.Add(new(IntakeFailureKind.Blind));

        if (TryComp<ImplantedComponent>(patient, out var implanted) && implanted.ImplantContainer.ContainedEntities.Count > 0)
            failures.Add(new(IntakeFailureKind.Implant, Name(implanted.ImplantContainer.ContainedEntities[0])));

        return failures;
    }

    private void CheckOrgans(BodyComponent body, IReadOnlyCollection<ProtoId<OrganCategoryPrototype>>? expectedParts, List<IntakeFailure> failures)
    {
        var present = new HashSet<ProtoId<OrganCategoryPrototype>>();

        foreach (var organ in body.Organs?.ContainedEntities ?? [])
        {
            if (!TryComp<OrganComponent>(organ, out var organComp))
                continue;

            if (organComp.Category is { } category)
                present.Add(category);

            var name = Name(organ);

            if (TryComp<WoundableComponent>(organ, out var woundable))
            {
                if (woundable.IntegrityCap - woundable.WoundableIntegrity > DamageTolerance)
                    failures.Add(new(IntakeFailureKind.LimbDamage, name));

                if (woundable.Bleeds > 0)
                    failures.Add(new(IntakeFailureKind.Bleeding, name));

                if (_trauma.HasWoundableTrauma(organ, woundableComp: woundable)
                    || woundable.Bone?.ContainedEntities.Any(b => TryComp<BoneComponent>(b, out var bone) && bone.BoneSeverity != BoneSeverity.Normal) == true)
                    failures.Add(new(IntakeFailureKind.Trauma, name));
            }

            if (TryComp<OrganIntegrityComponent>(organ, out var integrity) && integrity.OrganSeverity != OrganSeverity.Normal)
                failures.Add(new(IntakeFailureKind.OrganDamage, name));

            if (_surgery.HasUnfinishedSurgerySteps(organ))
                failures.Add(new(IntakeFailureKind.UnfinishedSurgery, name));
        }

        foreach (var expected in body.ExpectedOrgans.Concat(expectedParts ?? []).Distinct())
        {
            if (!present.Contains(expected))
                failures.Add(new(IntakeFailureKind.MissingPart, expected.Id));
        }
    }

    private void CheckReagents(EntityUid patient, BloodstreamComponent bloodstream, List<IntakeFailure> failures)
    {
        if (!_solution.ResolveSolution(patient, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var solution))
            return;

        foreach (var (reagent, _) in solution.Contents)
        {
            if (bloodstream.BloodReferenceSolution.Contents.Any(r => r.Reagent.Prototype == reagent.Prototype))
                continue;

            if (_proto.TryIndex<ReagentPrototype>(reagent.Prototype, out var proto) && IsHarmful(proto))
                failures.Add(new(IntakeFailureKind.HarmfulReagent, proto.LocalizedName));
        }
    }

    /// <summary>
    /// Harmful = its unconditional metabolism effects deal net damage. Overdose damage is
    /// conditional, so medicine at a normal dose passes.
    /// </summary>
    public static bool IsHarmful(ReagentPrototype reagent)
    {
        if (reagent.Metabolisms is null)
            return false;

        var net = FixedPoint2.Zero;
        foreach (var entry in reagent.Metabolisms.Metabolisms.Values)
        {
            foreach (var effect in entry.Effects)
            {
                if (effect is HealthChange health && effect.Conditions is not { Length: > 0 })
                    net += health.Damage.GetTotal();
            }
        }

        return net > 0;
    }
}
