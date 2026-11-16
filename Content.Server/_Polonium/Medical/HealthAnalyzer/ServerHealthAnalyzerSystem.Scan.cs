using Content.Server._Polonium.Body;
using Content.Shared._Polonium.Body;
using Content.Shared._Polonium.Medical.HealthAnalyzer;
using Content.Shared._Shitmed.Body;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Temperature.Components;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Utility;

namespace Content.Server.Medical.HealthAnalyzer;

/// <summary>
/// Polonium: the one-view health analyzer. Gathers everything about a patient into a single message.
/// </summary>
public sealed partial class ServerHealthAnalyzerSystem
{
    [Dependency] private CirculationSystem _circulation = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobThresholdSystem _mobThresholds = default!;

    /// <summary>
    /// Everything the analyzer shows about the target; an empty message when there is no patient with a body.
    /// </summary>
    public HealthAnalyzerScanMessage BuildScan(EntityUid? target, bool? scanActive)
    {
        var scan = new HealthAnalyzerScanMessage { ScanActive = scanActive };
        if (target is not { } patient || !TryComp<BodyComponent>(patient, out var body))
            return scan;

        scan.Target = GetNetEntity(patient);

        if (TryComp<TemperatureComponent>(patient, out var temperature))
            scan.Temperature = temperature.Temperature;

        if (TryComp<TemperatureDamageComponent>(patient, out var temperatureDamage))
        {
            scan.ColdDamageThreshold = temperatureDamage.ColdDamageThreshold;
            scan.HeatDamageThreshold = temperatureDamage.HeatDamageThreshold;
        }

        if (TryComp<BloodstreamComponent>(patient, out var bloodstream))
        {
            scan.BloodLevel = _bloodstreamSystem.GetBloodLevel(patient);
            scan.NormalBloodVolume = bloodstream.BloodReferenceSolution.Volume;
            scan.BloodVolume = scan.NormalBloodVolume * scan.BloodLevel;
            scan.BloodlossThreshold = bloodstream.BloodlossThreshold;
            scan.SystemicBleeding = bloodstream.BleedAmountNotFromWounds > 0;
        }

        scan.SpO2 = _circulation.ReadSpO2(patient);

        foreach (var (type, amount) in _damageable.GetAllDamage(patient).DamageDict)
        {
            if (amount > 0)
                scan.Damage[type] = amount;
        }

        if (_mobThresholds.TryGetThresholdForState(patient, MobState.Critical, out var crit))
            scan.CritThreshold = crit;
        if (_mobThresholds.TryGetThresholdForState(patient, MobState.Dead, out var dead))
            scan.DeadThreshold = dead;

        FetchLimbs(patient, body, scan);
        FetchOrgans(body, scan);

        scan.CirculationStopped = _circulation.IsStopped(patient);
        // The reasons the circulation sets (no heart, no brain) are hidden from the old analyzer's trait line, not from this.
        if (TryComp<UnrevivableComponent>(patient, out var unrevivable)
            && (unrevivable.Analyzable || HasComp<MissingVitalOrganComponent>(patient)))
            scan.Unrevivable = unrevivable.ReasonMessage;

        FetchChemicals(patient, body, scan);

        return scan;
    }

    private void FetchChemicals(EntityUid patient, BodyComponent body, HealthAnalyzerScanMessage scan)
    {
        Entity<SolutionComponent>? bloodEntity = null;
        var blood = TryComp<BloodstreamComponent>(patient, out var bloodstream)
                    && _solutionContainerSystem.ResolveSolution(patient, bloodstream.BloodSolutionName, ref bloodEntity);
        NetEntity? bloodNet = blood ? GetNetEntity(bloodEntity!.Value) : null;

        foreach (var (net, solution) in FetchChemicalData(patient, body))
        {
            if (net != bloodNet)
            {
                scan.Solutions.Add(solution);
                continue;
            }

            // The blood itself would fill the chart; only what is dissolved in it matters.
            var bloodReagents = new HashSet<string>();
            foreach (var reagent in bloodstream!.BloodReferenceSolution.Contents)
            {
                bloodReagents.Add(reagent.Reagent.Prototype);
            }

            var chemicals = new Solution();
            foreach (var reagent in solution.Solution.Contents)
            {
                if (!bloodReagents.Contains(reagent.Reagent.Prototype))
                    chemicals.AddReagent(reagent.Reagent, reagent.Quantity);
            }

            scan.BloodChemicals = chemicals;
        }
    }

    private void FetchLimbs(EntityUid patient, BodyComponent body, HealthAnalyzerScanMessage scan)
    {
        var bleeding = FetchBleedData(body);
        var tourniquets = FetchTourniquetData(body);
        var surgery = FetchUnfinishedSurgeryData(body);

        foreach (var (part, severity) in _woundSystem.GetDamageableStatesOnBody(patient))
        {
            scan.Limbs[part] = new AnalyzerLimbData
            {
                Severity = severity,
                Bleeding = bleeding.GetValueOrDefault(part),
                Tourniquet = tourniquets.GetValueOrDefault(part),
                UnfinishedSurgery = surgery.GetValueOrDefault(part),
                Damage = new(),
            };
        }

        if (body.Organs is null)
            return;

        foreach (var organ in body.Organs.ContainedEntities)
        {
            if (CompOrNull<OrganComponent>(organ)?.Category is not { } category
                || !LimbTargetMap.TryGetTarget(category, out var part)
                || !TryComp<WoundableComponent>(organ, out var woundable)
                || !scan.Limbs.TryGetValue(part, out var limb))
                continue;

            limb.IntegrityCap = woundable.IntegrityCap;
            if (TryComp<BoneComponent>(woundable.Bone?.ContainedEntities.FirstOrNull(), out var bone))
                limb.Bone = bone.BoneSeverity;

            foreach (var (type, amount) in _damageable.GetAllDamage(organ).DamageDict)
            {
                if (amount > 0)
                    limb.Damage[type] = amount;
            }

            scan.Limbs[part] = limb;
        }
    }

    private void FetchOrgans(BodyComponent body, HealthAnalyzerScanMessage scan)
    {
        if (body.Organs is not null)
        {
            foreach (var organ in body.Organs.ContainedEntities)
            {
                if (CompOrNull<OrganComponent>(organ)?.Category is not { } category)
                    continue;

                if (HasComp<DeadOrganComponent>(organ))
                {
                    scan.Organs.Add(new AnalyzerOrganData { Category = category, Dead = true, Severity = OrganSeverity.Destroyed });
                    continue;
                }

                if (!TryComp<OrganIntegrityComponent>(organ, out var integrity))
                    continue;

                scan.Organs.Add(new AnalyzerOrganData
                {
                    Category = category,
                    Integrity = integrity.OrganIntegrity,
                    IntegrityCap = integrity.IntegrityCap,
                    Severity = integrity.OrganSeverity,
                    FailureStage = CompOrNull<OrganFailureComponent>(organ)?.Stage ?? 0,
                });
            }
        }

        foreach (var category in FetchMissingOrgansData(body))
        {
            scan.Organs.Add(new AnalyzerOrganData { Category = category, Missing = true });
        }
    }
}
