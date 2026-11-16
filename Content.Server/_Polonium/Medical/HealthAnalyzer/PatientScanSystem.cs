using System.Linq;
using Content.Server.Body.Components;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Medical.HealthAnalyzer;
using Content.Shared.MedicalScanner;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Content.Server._Polonium.Body;
using Content.Shared._Polonium.Body;
using Content.Shared._Polonium.Medical.HealthAnalyzer;
using Content.Shared._Shitmed.Body;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
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

namespace Content.Server._Polonium.Medical.HealthAnalyzer;

/// <summary>
/// The one-view health analyzer: gathers everything about a patient into a single message and sends it in place of
/// the upstream analyzer state, for the handheld analyzer, the MedTek cartridge and the body scanner console.
/// </summary>
public sealed partial class PatientScanSystem : EntitySystem
{
    [Dependency] private BloodstreamSystem _bloodstreamSystem = default!;
    [Dependency] private CirculationSystem _circulation = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobThresholdSystem _mobThresholds = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainerSystem = default!;
    [Dependency] private SharedSurgerySystem _surgerySystem = default!;
    [Dependency] private UserInterfaceSystem _uiSystem = default!;
    [Dependency] private WoundSystem _woundSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HealthAnalyzerComponent, HealthAnalyzerUpdateUiEvent>(OnUpdateUi);
    }

    private void OnUpdateUi(Entity<HealthAnalyzerComponent> analyzer, ref HealthAnalyzerUpdateUiEvent args)
    {
        args.Handled = true;
        SendScan(analyzer, HealthAnalyzerUiKey.Key, analyzer.Comp.ScannedEntity, analyzer.Comp.IsAnalyzerActive);
    }

    /// <summary>
    /// Sends the whole scan of the target to the analyzer window on the given entity.
    /// </summary>
    public void SendScan(EntityUid uiEntity, Enum uiKey, EntityUid? target, bool scanActive)
    {
        if (!_uiSystem.HasUi(uiEntity, uiKey))
            return;

        _uiSystem.ServerSendUiMessage(uiEntity, uiKey, BuildScan(target, scanActive));
    }

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
        scan.Lobotomized = body.Organs?.ContainedEntities.Any(HasComp<LobotomizedComponent>) ?? false;
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

    private Dictionary<TargetBodyPart, bool> FetchBleedData(BodyComponent body)
    {
        var bleeding = new Dictionary<TargetBodyPart, bool>();

        if (body.Organs is null)
            return bleeding;

        foreach (var organ in body.Organs.ContainedEntities)
        {
            if (!TryComp<OrganComponent>(organ, out var organComp)
                || organComp.Category is not { } category
                || !LimbTargetMap.TryGetTarget(category, out var targetPart)
                || !TryComp<WoundableComponent>(organ, out var woundable))
                continue;

            bleeding[targetPart] = woundable.Bleeds > 0;
        }

        return bleeding;
    }

    private Dictionary<TargetBodyPart, bool> FetchTourniquetData(BodyComponent body)
    {
        var tourniqueted = new Dictionary<TargetBodyPart, bool>();

        if (body.Organs is null)
            return tourniqueted;

        foreach (var organ in body.Organs.ContainedEntities)
        {
            if (!TryComp<OrganComponent>(organ, out var organComp)
                || organComp.Category is not { } category
                || !LimbTargetMap.TryGetTarget(category, out var targetPart)
                || !TryComp<WoundableComponent>(organ, out var woundable))
                continue;

            var hasTourniquet = false;
            foreach (var wound in _woundSystem.GetWoundableWounds(organ, woundable))
            {
                if (!TryComp<BleedInflicterComponent>(wound, out var bleedInflicter))
                    continue;

                if (bleedInflicter.BleedingModifiers.ContainsKey("TourniquetPresent"))
                {
                    hasTourniquet = true;
                    break;
                }
            }

            tourniqueted[targetPart] = hasTourniquet;
        }

        return tourniqueted;
    }

    private Dictionary<TargetBodyPart, bool> FetchUnfinishedSurgeryData(BodyComponent body)
    {
        var unfinished = new Dictionary<TargetBodyPart, bool>();

        if (body.Organs is null)
            return unfinished;

        foreach (var organ in body.Organs.ContainedEntities)
        {
            if (!TryComp<OrganComponent>(organ, out var organComp)
                || organComp.Category is not { } category
                || !LimbTargetMap.TryGetTarget(category, out var targetPart))
                continue;

            unfinished[targetPart] = _surgerySystem.HasUnfinishedSurgerySteps(organ);
        }

        return unfinished;
    }

    private List<ProtoId<OrganCategoryPrototype>> FetchMissingOrgansData(BodyComponent body)
    {
        var missing = new List<ProtoId<OrganCategoryPrototype>>();

        if (body.ExpectedOrgans.Count == 0)
            return missing;

        var present = new HashSet<ProtoId<OrganCategoryPrototype>>();
        if (body.Organs is not null)
        {
            foreach (var organ in body.Organs.ContainedEntities)
            {
                if (TryComp<OrganComponent>(organ, out var organComp) && organComp.Category is { } category)
                    present.Add(category);
            }
        }

        foreach (var expected in body.ExpectedOrgans)
        {
            if (!present.Contains(expected))
                missing.Add(expected);
        }

        return missing;
    }

    private Dictionary<NetEntity, NamedSolution> FetchChemicalData(EntityUid target, BodyComponent body)
    {
        var solutions = new Dictionary<NetEntity, NamedSolution>();

        if (TryComp<SolutionManagerComponent>(target, out var container))
        {
            foreach (var (name, solution) in _solutionContainerSystem.EnumerateSolutions((target, container)))
            {
                if (name is null || name == BloodstreamComponent.DefaultBloodTemporarySolutionName)
                    continue;

                var displayName = name switch
                {
                    BloodstreamComponent.DefaultBloodSolutionName => Loc.GetString("group-solution-name-bloodstream"),
                    BloodstreamComponent.DefaultMetabolitesSolutionName => Loc.GetString("group-solution-name-metabolites"),
                    "print" => Loc.GetString("group-solution-name-print"),
                    _ => name,
                };
                solutions[GetNetEntity(solution)] = new NamedSolution(displayName, solution.Comp.Solution);
            }
        }

        if (body.Organs is null)
            return solutions;

        foreach (var organ in body.Organs.ContainedEntities)
        {
            if (!TryComp<StomachComponent>(organ, out var stomach) || stomach.Solution is not { } stomachSolution)
                continue;

            var organName = MetaData(organ).EntityName;
            solutions[GetNetEntity(stomachSolution)] = new NamedSolution(organName, stomachSolution.Comp.Solution);
        }

        return solutions;
    }
}
