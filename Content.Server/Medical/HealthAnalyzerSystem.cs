// SPDX-FileCopyrightText: 2022 Fishfish458 <47410468+Fishfish458@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 Rane <60792108+Elijahrane@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 fishfish458 <fishfish458>
// SPDX-FileCopyrightText: 2023 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 DrSmugleaf <drsmugleaf@gmail.com>
// SPDX-FileCopyrightText: 2023 Emisse <99158783+Emisse@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Jezithyr <jezithyr@gmail.com>
// SPDX-FileCopyrightText: 2023 Kara <lunarautomaton6@gmail.com>
// SPDX-FileCopyrightText: 2023 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Pieter-Jan Briers <pieterjan.briers@gmail.com>
// SPDX-FileCopyrightText: 2023 TemporalOroboros <TemporalOroboros@gmail.com>
// SPDX-FileCopyrightText: 2023 keronshb <54602815+keronshb@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 nmajask <nmajask@gmail.com>
// SPDX-FileCopyrightText: 2024 ArchRBX <5040911+ArchRBX@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Brandon Hu <103440971+Brandon-Huu@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Cojoke <83733158+Cojoke-dot@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Milon <plmilonpl@gmail.com>
// SPDX-FileCopyrightText: 2024 Plykiya <58439124+Plykiya@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Rainfey <rainfey0+github@gmail.com>
// SPDX-FileCopyrightText: 2024 Saphire Lattice <lattice@saphi.re>
// SPDX-FileCopyrightText: 2024 Whisper <121047731+QuietlyWhisper@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 deltanedas <39013340+deltanedas@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 deltanedas <@deltanedas:kde.org>
// SPDX-FileCopyrightText: 2024 lzk <124214523+lzk228@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 nikthechampiongr <32041239+nikthechampiongr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Hannah Giovanna Dawson <karakkaraz@gmail.com>
// SPDX-FileCopyrightText: 2025 Minemoder5000 <minemoder50000@gmail.com>
// SPDX-FileCopyrightText: 2025 Nikovnik <116634167+nkokic@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 PJB3005 <pieterjan.briers+git@gmail.com>
// SPDX-FileCopyrightText: 2025 Princess Cheeseballs <66055347+Princess-Cheeseballs@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Vasilis The Pikachu <vasilis@pikachu.systems>
// SPDX-FileCopyrightText: 2025 Zachary Higgs <compgeek223@gmail.com>
// SPDX-FileCopyrightText: 2025 slarticodefast <161409025+slarticodefast@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Fruitsalad <949631+Fruitsalad@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Maciej Walendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
// SPDX-FileCopyrightText: 2026 Vanessa <908648+ShepardToTheStars@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 maciejwalendziuk <15122746+maciejwalendziuk@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 taydeo <tay@funkystation.org>
// SPDX-FileCopyrightText: 2026 taydeo <td12233a@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server._Shitmed.PartStatus;
using Content.Server.BaseAnalyzer;
using Content.Server.Body.Systems;
using Content.Server.Medical.Components;
using Content.Shared._Shitmed.Body;
using Content.Shared._Shitmed.Medical.HealthAnalyzer;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.MedicalScanner;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Temperature.Components;
using Content.Shared.Traits.Assorted;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
namespace Content.Server.Medical;

public sealed partial class HealthAnalyzerSystem : BaseAnalyzerSystem<HealthAnalyzerComponent, HealthAnalyzerDoAfterEvent>
{
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainerSystem = default!;
    [Dependency] private UserInterfaceSystem _uiSystem = default!;
    [Dependency] private BloodstreamSystem _bloodstreamSystem = default!;
    [Dependency] private PartStatusSystem _partStatus = default!;
    [Dependency] private WoundSystem _woundSystem = default!;
    [Dependency] private TraumaSystem _traumaSystem = default!;
    [Dependency] private SharedSurgerySystem _surgerySystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<HealthAnalyzerComponent>(HealthAnalyzerUiKey.Key, subs =>
        {
            subs.Event<HealthAnalyzerModeSelectedMessage>(OnModeSelected);
            subs.Event<HealthAnalyzerPartSelectedMessage>(OnPartSelected);
        });
    }

    protected override void HandleOutOfScanRange(Entity<HealthAnalyzerComponent> analyzer, EntityUid target)
    {
        PauseAnalyzingEntity(analyzer, target);
    }

    protected override void HandleInScanRange(Entity<HealthAnalyzerComponent> analyzer, EntityUid target)
    {
        analyzer.Comp.IsAnalyzerActive = true;
        UpdateScannedUser(analyzer, target, true, analyzer.Comp.CurrentBodyPart);
    }

    public override void BeginAnalyzingEntity(Entity<HealthAnalyzerComponent> healthAnalyzer, EntityUid target, EntityUid? part = null)
    {
        healthAnalyzer.Comp.ScannedEntity = target;
        healthAnalyzer.Comp.CurrentBodyPart = part;

        _toggle.TryActivate(healthAnalyzer.Owner);

        UpdateScannedUser(healthAnalyzer, target, true, part);
    }

    private void PauseAnalyzingEntity(Entity<HealthAnalyzerComponent> healthAnalyzer, EntityUid target)
    {
        if (!healthAnalyzer.Comp.IsAnalyzerActive)
            return;

        UpdateScannedUser(healthAnalyzer, target, false, healthAnalyzer.Comp.CurrentBodyPart);
        healthAnalyzer.Comp.IsAnalyzerActive = false;
    }

    private void OnModeSelected(Entity<HealthAnalyzerComponent> ent, ref HealthAnalyzerModeSelectedMessage args)
    {
        ent.Comp.CurrentMode = args.Mode;
        ent.Comp.CurrentBodyPart = null;

        if (ent.Comp.ScannedEntity is { } target)
            UpdateScannedUser(ent, target, ent.Comp.IsAnalyzerActive, null);
    }

    private void OnPartSelected(Entity<HealthAnalyzerComponent> ent, ref HealthAnalyzerPartSelectedMessage args)
    {
        if (ent.Comp.ScannedEntity is not { } target)
            return;

        ent.Comp.CurrentMode = HealthAnalyzerMode.Body;

        EntityUid? part = null;
        if (args.BodyPart is { } bodyPart
            && LimbTargetMap.TryGetCategory(bodyPart, out var category)
            && TryComp<BodyComponent>(target, out var body)
            && LimbTargetMap.TryGetOrganByCategory(EntityManager, body, category, out var organ))
            part = organ;

        ent.Comp.CurrentBodyPart = part;
        UpdateScannedUser(ent, target, ent.Comp.IsAnalyzerActive, part);
    }

    public override void UpdateScannedUser(EntityUid healthAnalyzer, EntityUid target, bool scanMode, EntityUid? part = null)
    {
        if (!TryComp<HealthAnalyzerComponent>(healthAnalyzer, out var analyzerComp))
            return;

        SendAnalyzerUiState(healthAnalyzer, HealthAnalyzerUiKey.Key, target, analyzerComp.CurrentMode, part, scanMode);
    }

    public void SendAnalyzerUiState(EntityUid uiEntity, Enum uiKey, EntityUid? target, HealthAnalyzerMode mode, EntityUid? part, bool scanMode)
    {
        if (!_uiSystem.HasUi(uiEntity, uiKey))
            return;

        // No patient to show (console not linked, linked bed empty, or target lost its body) -
        // a null TargetEntity is exactly what the client's TrySetupEntity already treats as "no
        // patient data", so this reuses the handheld analyzer's own empty state instead of
        // needing a bespoke placeholder.
        if (target is not { } validTarget || !TryComp<BodyComponent>(validTarget, out var body))
        {
            _uiSystem.ServerSendUiMessage(uiEntity, uiKey, new HealthAnalyzerBodyMessage(
                null, float.NaN, float.NaN, null, false, null,
                new(), new(), new(), new(), new(), null));
            return;
        }

        var bodyTemperature = float.NaN;
        if (TryComp<TemperatureComponent>(validTarget, out var temp))
            bodyTemperature = temp.CurrentTemperature;

        var bloodAmount = float.NaN;
        if (HasComp<BloodstreamComponent>(validTarget))
            bloodAmount = _bloodstreamSystem.GetBloodLevel(validTarget);

        var bodyStatus = _woundSystem.GetDamageableStatesOnBody(validTarget);
        var bleeding = FetchBleedData(body);
        var tourniqueted = FetchTourniquetData(body);
        var unfinishedSurgery = FetchUnfinishedSurgeryData(body);
        var missingOrgans = FetchMissingOrgansData(body);

        switch (mode)
        {
            case HealthAnalyzerMode.Organs:
                _uiSystem.ServerSendUiMessage(uiEntity, uiKey, new HealthAnalyzerOrgansMessage(
                    GetNetEntity(validTarget), bodyTemperature, bloodAmount, scanMode, bodyStatus, bleeding, tourniqueted, unfinishedSurgery, missingOrgans,
                    FetchOrganData(body)));
                break;

            case HealthAnalyzerMode.Chemicals:
                _uiSystem.ServerSendUiMessage(uiEntity, uiKey, new HealthAnalyzerChemicalsMessage(
                    GetNetEntity(validTarget), bodyTemperature, bloodAmount, scanMode, bodyStatus, bleeding, tourniqueted, unfinishedSurgery, missingOrgans,
                    FetchChemicalData(validTarget, body)));
                break;

            case HealthAnalyzerMode.Body:
            default:
                var unrevivable = TryComp<UnrevivableComponent>(validTarget, out var unrevivableComp) && unrevivableComp.Analyzable;

                _uiSystem.ServerSendUiMessage(uiEntity, uiKey, new HealthAnalyzerBodyMessage(
                    GetNetEntity(validTarget), bodyTemperature, bloodAmount, scanMode, unrevivable, bodyStatus, bleeding, tourniqueted, unfinishedSurgery, missingOrgans,
                    FetchTraumaData(body), part != null ? GetNetEntity(part.Value) : null));
                break;
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

    private Dictionary<NetEntity, List<WoundableTraumaData>> FetchTraumaData(BodyComponent body)
    {
        var traumas = new Dictionary<NetEntity, List<WoundableTraumaData>>();

        if (body.Organs is null)
            return traumas;

        foreach (var organ in body.Organs.ContainedEntities)
        {
            if (!HasComp<WoundableComponent>(organ))
                continue;

            traumas[GetNetEntity(organ)] = FetchTraumaData(organ);
        }

        return traumas;
    }

    private List<WoundableTraumaData> FetchTraumaData(EntityUid woundable)
    {
        var traumasList = new List<WoundableTraumaData>();

        if (!_traumaSystem.TryGetWoundableTrauma(woundable, out var traumasFound))
            return traumasList;

        foreach (var trauma in traumasFound)
        {
            if (trauma.Comp.TraumaType == TraumaType.BoneDamage
                && trauma.Comp.TraumaTarget is { } bone
                && TryComp(bone, out BoneComponent? boneComp))
            {
                traumasList.Add(new WoundableTraumaData(
                    trauma.Comp.TraumaType.ToString(), trauma.Comp.TraumaSeverity, boneComp.BoneSeverity.ToString()));
                continue;
            }

            traumasList.Add(new WoundableTraumaData(trauma.Comp.TraumaType.ToString(), trauma.Comp.TraumaSeverity));
        }

        return traumasList;
    }

    private Dictionary<NetEntity, OrganTraumaData> FetchOrganData(BodyComponent body)
    {
        var organs = new Dictionary<NetEntity, OrganTraumaData>();

        if (body.Organs is null)
            return organs;

        foreach (var organ in body.Organs.ContainedEntities)
        {
            if (!TryComp<OrganIntegrityComponent>(organ, out var integrity))
                continue;

            organs[GetNetEntity(organ)] = new OrganTraumaData(
                integrity.OrganIntegrity,
                integrity.IntegrityCap,
                integrity.OrganSeverity,
                integrity.IntegrityModifiers.Select(x => (x.Key.Item1, x.Value)).ToList());
        }

        return organs;
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

    public HealthAnalyzerUiState GetHealthAnalyzerUiState(EntityUid? target)
    {
        if (!target.HasValue || !HasComp<DamageableComponent>(target))
            return new HealthAnalyzerUiState();

        var entity = target.Value;
        var bodyTemperature = float.NaN;

        if (TryComp<TemperatureComponent>(entity, out var temp))
            bodyTemperature = temp.CurrentTemperature;

        var bloodAmount = float.NaN;
        var bleeding = false;
        var unrevivable = false;

        if (TryComp<BloodstreamComponent>(entity, out var bloodstream) &&
            _solutionContainerSystem.ResolveSolution(entity, bloodstream.BloodSolutionName,
                ref bloodstream.BloodSolution, out _))
        {
            bloodAmount = _bloodstreamSystem.GetBloodLevel(entity);
            bleeding = bloodstream.BleedAmount > 0;
        }

        if (TryComp<UnrevivableComponent>(entity, out var unrevivableComp) && unrevivableComp.Analyzable)
            unrevivable = true;

        Dictionary<TargetBodyPart, string>? partStatuses = null;
        if (TryComp<BodyComponent>(entity, out var bodyComp) && bodyComp.Organs is not null)
            partStatuses = _partStatus.GetPartStatusDescriptions(entity);

        return new HealthAnalyzerUiState(
            GetNetEntity(entity),
            bodyTemperature,
            bloodAmount,
            null,
            bleeding,
            unrevivable,
            partStatuses
        );
    }

    protected override Enum GetUiKey()
    {
        return HealthAnalyzerUiKey.Key;
    }

    protected override bool ScanTargetPopupMessage(Entity<HealthAnalyzerComponent> uid, AfterInteractEvent args, [NotNullWhen(true)] out string? message)
    {
        message = Loc.GetString("health-analyzer-popup-scan-target", ("user", Identity.Entity(args.User, EntityManager)));
        return true;
    }

    protected override bool ValidScanTarget(EntityUid? target)
    {
        return HasComp<MobStateComponent>(target) && HasComp<BodyComponent>(target);
    }
}
