using System.Linq;
using Content.Server._Shitmed.PartStatus;
using Content.Shared._Shitmed.Body;
using Content.Shared._Shitmed.Medical.HealthAnalyzer;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Medical.HealthAnalyzer;
using Content.Shared.MedicalScanner;
using Content.Shared.Temperature.Components;
using Content.Shared.Traits.Assorted;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.Medical.HealthAnalyzer;

/// <inheritdoc/>
public sealed partial class ServerHealthAnalyzerSystem : HealthAnalyzerSystem
{
    [Dependency] private EntityQuery<TemperatureComponent> _temperatureQuery;

    // Polonium start: Shitmed readout
    [Dependency] private SharedSolutionContainerSystem _solutionContainerSystem = default!;
    [Dependency] private UserInterfaceSystem _uiSystem = default!;
    [Dependency] private BloodstreamSystem _bloodstreamSystem = default!;
    [Dependency] private PartStatusSystem _partStatus = default!;
    [Dependency] private WoundSystem _woundSystem = default!;
    [Dependency] private SharedSurgerySystem _surgerySystem = default!;
    // Polonium end

    public override HealthAnalyzerUiState GetHealthAnalyzerUiState(EntityUid? target, bool scanMode)
    {
        var state = base.GetHealthAnalyzerUiState(target, scanMode);

        if (_temperatureQuery.TryComp(target, out var temp))
            state.Temperature = temp.Temperature;

        // Polonium: Shitmed part statuses
        if (target is { } entity && TryComp<BodyComponent>(entity, out var bodyComp) && bodyComp.Organs is not null)
            state.PartStatuses = _partStatus.GetPartStatusDescriptions(entity);

        return state;
    }

    // Polonium start: Shitmed readout
    protected override void UpdateUi(Entity<HealthAnalyzerComponent> analyzer)
    {
        SendAnalyzerUiState(analyzer,
            HealthAnalyzerUiKey.Key,
            analyzer.Comp.ScannedEntity,
            analyzer.Comp.CurrentMode,
            analyzer.Comp.CurrentBodyPart,
            analyzer.Comp.IsAnalyzerActive);
    }

    public void SendAnalyzerUiState(EntityUid uiEntity, Enum uiKey, EntityUid? target, HealthAnalyzerMode mode, EntityUid? part, bool scanMode)
    {
        if (!_uiSystem.HasUi(uiEntity, uiKey))
            return;

        // One view instead of tabs: the whole scan goes out every time, so the mode and part are not used.
        _uiSystem.ServerSendUiMessage(uiEntity, uiKey, BuildScan(target, scanMode));
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
    // Polonium end
}
