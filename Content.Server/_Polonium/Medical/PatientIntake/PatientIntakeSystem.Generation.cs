// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._Polonium.Medical.PatientIntake;
using Content.Shared._Shitmed.Body;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Inventory;
using Content.Shared.Random.Helpers;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Medical.PatientIntake;

public sealed partial class PatientIntakeSystem
{
    [Dependency] private HumanoidProfileSystem _humanoidProfile = default!;
    [Dependency] private OrganRelationSystem _organRelations = default!;
    [Dependency] private SharedVisualBodySystem _visualBody = default!;

    /// <summary>
    /// Damage type and amount of the wound that owns a generated bone or organ trauma.
    /// </summary>
    private const string InducedWoundType = "Blunt";

    private static readonly FixedPoint2 InducedWoundDamage = FixedPoint2.New(5);

    /// <summary>
    /// Spawns a dressed, injured, frozen patient for the machine's active case.
    /// </summary>
    public EntityUid SpawnPatient(Entity<PatientIntakeMachineComponent> machine, PatientIntakeCasePrototype caseProto, EntityCoordinates coords)
    {
        var patient = SpawnRandomPatient(machine.Comp.Humanoid, coords);
        var comp = AddComp<PatientIntakePatientComponent>(patient);
        comp.Machine = machine;
        Dirty(patient, comp);

        if (machine.Comp.Outfits.Count > 0)
            Dress(patient, _random.Pick(machine.Comp.Outfits.ToDictionary(o => _proto.Index(o), o => _proto.Index(o).Weight)));

        if (TryComp<HumanoidProfileComponent>(patient, out var profile)
            && machine.Comp.SpeciesGear.TryGetValue(profile.Species, out var speciesGear))
        {
            _spawning.EquipStartingGear(patient, speciesGear);
            ConnectInternals(patient);
        }

        comp.Gear = _inventory.GetHandOrInventoryEntities(patient).ToList();
        comp.ExpectedParts = PartCategories(patient);

        if (caseProto.Issues is { } fixedIssues)
        {
            foreach (var id in fixedIssues)
            {
                if (ApplyIssue(patient, comp, _proto.Index(id)))
                    comp.Issues.Add(id);
            }
        }
        else
        {
            RollIssues(patient, comp, caseProto);
        }

        return patient;
    }

    /// <summary>
    /// A random roundstart species outside the settings' blacklist, with a random look and name.
    /// HumanoidCharacterProfile.Random would re-roll the species and ignore the blacklist.
    /// </summary>
    private EntityUid SpawnRandomPatient(ProtoId<RandomHumanoidSettingsPrototype> settingsId, EntityCoordinates coords)
    {
        var settings = _proto.Index(settingsId);
        var species = _random.Pick(_proto.EnumeratePrototypes<SpeciesPrototype>()
            .Where(s => s.RoundStart && !settings.SpeciesBlacklist.Contains(s.ID))
            .ToList());

        var profile = HumanoidCharacterProfile.RandomWithSpecies(species.ID);
        var patient = Spawn(species.Prototype, coords);
        _metaData.SetEntityName(patient, profile.Name);
        _visualBody.ApplyProfileTo(patient, profile);
        _humanoidProfile.ApplyProfileTo(patient, profile);
        return patient;
    }

    /// <summary>
    /// Rolls every slot of the group separately, so one group makes many different outfits.
    /// </summary>
    private void Dress(EntityUid patient, PatientIntakeOutfitPrototype outfit)
    {
        var coords = Transform(patient).Coordinates;
        foreach (var (slot, options) in outfit.Slots)
        {
            if (options.Items.Count == 0 || !_random.Prob(options.Chance))
                continue;

            var item = Spawn(_random.Pick(options.Items), coords);
            if (!_inventory.TryEquip(patient, item, slot, silent: true, force: true))
                Del(item); // e.g. shoes on a species that can't wear them
        }
    }

    private void RollIssues(EntityUid patient, PatientIntakePatientComponent comp, PatientIntakeCasePrototype caseProto)
    {
        var pool = _proto.EnumeratePrototypes<PatientIntakeIssuePrototype>()
            .Where(i => i.Kind != PatientIntakeIssueKind.ReplacePart
                && (caseProto.AllowMissingParts || i.Kind != PatientIntakeIssueKind.RemovePart))
            .ToList();

        var budget = caseProto.Budget;
        while (budget > 0)
        {
            var weights = pool.Where(i => i.Cost <= budget).ToDictionary(i => i, i => i.Weight);
            if (weights.Count == 0)
                break;

            var issue = _random.Pick(weights);
            if (!ApplyIssue(patient, comp, issue))
            {
                // Nothing on this body it can apply to.
                pool.Remove(issue);
                continue;
            }

            comp.Issues.Add(issue.ID);
            budget -= issue.Cost;
        }
    }

    public bool ApplyIssue(EntityUid patient, PatientIntakePatientComponent comp, PatientIntakeIssuePrototype issue)
    {
        switch (issue.Kind)
        {
            case PatientIntakeIssueKind.Damage:
            {
                if (issue.Damage is null || !TryPickLimb(patient, _ => true, out var limb))
                    return false;

                _damageable.TryChangeDamage(limb.Owner, issue.Damage, ignoreResistances: true, interruptsDoAfters: false);
                return true;
            }

            case PatientIntakeIssueKind.Bleed:
            {
                if (issue.Damage is null || !TryPickLimb(patient, _ => true, out var limb))
                    return false;

                _damageable.TryChangeDamage(limb.Owner, issue.Damage, ignoreResistances: true, interruptsDoAfters: false);
                _bloodstream.TryModifyBloodLevel(patient, -issue.Severity);
                return true;
            }

            case PatientIntakeIssueKind.Bone:
            {
                if (!TryPickLimb(patient, l => l.Comp.Bone?.ContainedEntities.Count > 0, out var limb)
                    || InduceWound(limb) is not { } inflicter)
                    return false;

                // Same path a real fracture takes, so mend-bones surgery treats it.
                return _trauma.ApplyBoneTrauma(limb.Comp.Bone!.ContainedEntities[0], limb, inflicter, issue.Severity);
            }

            case PatientIntakeIssueKind.Organ:
            {
                if (!TryPickOrgan(patient, issue, o => HasComp<OrganIntegrityComponent>(o), out var organ, out var category)
                    || !TryGetHousingLimb(patient, category, out var limb)
                    || InduceWound(limb) is not { } inflicter)
                    return false;

                // Same path a real organ trauma takes: surgery only heals modifiers owned by a trauma.
                var trauma = _trauma.AddTrauma(organ, limb, inflicter, TraumaType.OrganDamage, issue.Severity);
                return trauma != EntityUid.Invalid
                    && _trauma.TryCreateOrganDamageModifier(organ, issue.Severity, trauma, "WoundableDamage");
            }

            case PatientIntakeIssueKind.Poison:
            {
                if (issue.Reagent is not { } reagent
                    || !TryComp<BloodstreamComponent>(patient, out var bloodstream)
                    || !_solutions.ResolveSolution(patient, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution))
                    return false;

                return _solutions.TryAddReagent(bloodstream.BloodSolution.Value, reagent, issue.Quantity);
            }

            case PatientIntakeIssueKind.Blind:
                _blindable.AdjustEyeDamage(patient, issue.EyeDamage);
                return true;

            case PatientIntakeIssueKind.RemovePart:
            {
                _trauma.TryGetBodyTraumas(patient, out var traumas);
                if (!TryPickOrgan(patient, issue, o => traumas?.All(t => t.Comp.TraumaTarget != o && t.Comp.HoldingWoundable != o) ?? true,
                        out var organ, out var category)
                    || !TryComp<BodyComponent>(patient, out var body)
                    || body.Organs is null)
                    return false;

                comp.IssueParts[issue.ID] = category;

                // Woundable limbs go through amputation so the parent gets the dismemberment
                // trauma reattach surgery expects.
                if (TryComp<WoundableComponent>(organ, out var woundable))
                {
                    _wound.AmputateWoundableSafely(organ, woundable);
                }
                else
                {
                    // Hands and feet aren't woundable: detach like dismemberment does, minus the
                    // trauma, so the attach-hand/foot surgery is available straight away.
                    if (HasComp<ChildOrganComponent>(organ))
                        _organRelations.Orphan(organ);

                    _container.Remove(organ, body.Organs, force: true);
                }

                Del(organ);
                return true;
            }

            case PatientIntakeIssueKind.ReplacePart:
            {
                if (!TryPickOrgan(patient, issue, _ => true, out var organ, out var category))
                    return false;

                comp.Replace[category] = organ;
                comp.IssueParts[issue.ID] = category;
                if (issue.Prosthetic)
                    comp.Prosthetic.Add(category);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Every organ category in the body; hands and feet aren't woundable, so this is wider than the limbs.
    /// </summary>
    public HashSet<ProtoId<OrganCategoryPrototype>> PartCategories(EntityUid patient)
    {
        var categories = new HashSet<ProtoId<OrganCategoryPrototype>>();
        if (!TryComp<BodyComponent>(patient, out var body) || body.Organs is null)
            return categories;

        foreach (var organ in body.Organs.ContainedEntities)
        {
            if (TryComp<OrganComponent>(organ, out var organComp) && organComp.Category is { } category)
                categories.Add(category);
        }

        return categories;
    }

    private IEnumerable<(Entity<WoundableComponent> Limb, ProtoId<OrganCategoryPrototype> Category)> WoundableParts(EntityUid patient)
    {
        if (!TryComp<BodyComponent>(patient, out var body) || body.Organs is null)
            yield break;

        foreach (var organ in body.Organs.ContainedEntities)
        {
            if (TryComp<WoundableComponent>(organ, out var woundable)
                && TryComp<OrganComponent>(organ, out var organComp)
                && organComp.Category is { } category)
                yield return ((organ, woundable), category);
        }
    }

    private bool TryPickLimb(EntityUid patient, Func<Entity<WoundableComponent>, bool> filter, out Entity<WoundableComponent> limb)
    {
        var limbs = WoundableParts(patient).Select(p => p.Limb).Where(filter).ToList();
        limb = limbs.Count > 0 ? _random.Pick(limbs) : default;
        return limbs.Count > 0;
    }

    private bool TryPickOrgan(
        EntityUid patient,
        PatientIntakeIssuePrototype issue,
        Func<EntityUid, bool> filter,
        out EntityUid organ,
        out ProtoId<OrganCategoryPrototype> category)
    {
        organ = default;
        category = default;
        if (!TryComp<BodyComponent>(patient, out var body) || body.Organs is null)
            return false;

        var candidates = body.Organs.ContainedEntities
            .Where(o => TryComp<OrganComponent>(o, out var c) && c.Category is { } cat && issue.Categories.Contains(cat) && filter(o))
            .ToList();

        if (candidates.Count == 0)
            return false;

        organ = _random.Pick(candidates);
        category = Comp<OrganComponent>(organ).Category!.Value;
        return true;
    }

    private bool TryGetHousingLimb(EntityUid patient, ProtoId<OrganCategoryPrototype> organCategory, out Entity<WoundableComponent> limb)
    {
        foreach (var (part, category) in WoundableParts(patient))
        {
            if (!LimbTargetMap.GetVitalOrganCategories(category).Contains(organCategory))
                continue;

            limb = part;
            return true;
        }

        limb = default;
        return false;
    }

    /// <summary>
    /// A small real wound on the limb to own the trauma. Made through actual damage, so the wound
    /// has damage behind it and ordinary brute medicine heals it; an induced wound would not.
    /// </summary>
    private Entity<TraumaInflicterComponent>? InduceWound(Entity<WoundableComponent> limb)
    {
        _damageable.TryChangeDamage(limb.Owner, new DamageSpecifier(_proto.Index<DamageTypePrototype>(InducedWoundType), InducedWoundDamage),
            ignoreResistances: true, interruptsDoAfters: false);

        foreach (var wound in _wound.GetWoundableWounds(limb, limb.Comp))
        {
            if (MetaData(wound).EntityPrototype?.ID == $"Wound{InducedWoundType}" && !wound.Comp.IsScar)
                return (wound.Owner, EnsureComp<TraumaInflicterComponent>(wound.Owner));
        }

        return null;
    }

    private void ConnectInternals(EntityUid patient)
    {
        if (!TryComp<InternalsComponent>(patient, out var internals) || _internals.FindBestGasTank(patient) is not { } tank)
            return;

        _internals.TryConnectTank((patient, internals), tank);
    }
}
