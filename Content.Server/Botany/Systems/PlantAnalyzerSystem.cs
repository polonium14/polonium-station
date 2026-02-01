// SPDX-FileCopyrightText: 2025 Mehnix <56132549+Mehnix@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.BaseAnalyzer;
using Content.Server.Botany.Components;
using Content.Server.Popups;
using Content.Shared.Botany.PlantAnalyzer;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Labels.EntitySystems;
using Content.Shared.Paper;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;


namespace Content.Server.Botany.Systems;

public sealed class PlantAnalyzerSystem : BaseAnalyzerSystem<PlantAnalyzerComponent, PlantAnalyzerDoAfterEvent>
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly PaperSystem _paperSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedLabelSystem _labelSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlantAnalyzerComponent, PlantAnalyzerPrintMessage>(OnPrint);

    }

    /// <inheritdoc/>
    public override void UpdateScannedUser(EntityUid analyzer, EntityUid target, bool scanMode, EntityUid? part = null)
    {

        if (!UiSystem.HasUi(analyzer, PlantAnalyzerUiKey.Key))
            return;

        if (!ValidScanTarget(target))
            return;

        if (!_entityManager.TryGetComponent<PlantAnalyzerComponent>(analyzer, out var analyzerComponent))
            return;

        UiSystem.ServerSendUiMessage(analyzer, PlantAnalyzerUiKey.Key, GatherData(analyzerComponent, scanMode, target: target));
    }

    private PlantAnalyzerScannedUserMessage GatherData(PlantAnalyzerComponent analyzer, bool? scanMode = null, EntityUid? target = null)
    {
        target ??= analyzer.ScannedEntity;
        PlantAnalyzerPlantData? plantData = null;
        PlantAnalyzerTrayData? trayData = null;
        PlantAnalyzerTolerancesData? tolerancesData = null;
        PlantAnalyzerProduceData? produceData = null;
        if (_entityManager.TryGetComponent<PlantHolderComponent>(target, out var plantHolder))
        {
            if (plantHolder.Seed is not null)
            {
                plantData = new PlantAnalyzerPlantData(
                    seedDisplayName: plantHolder.Seed.DisplayName,
                    health: plantHolder.Health,
                    endurance: plantHolder.Seed.Endurance,
                    age: plantHolder.Age,
                    lifespan: plantHolder.Seed.Lifespan,
                    dead: plantHolder.Dead,
                    viable: plantHolder.Seed.Viable,
                    mutating: plantHolder.MutationLevel > 0f,
                    kudzu: plantHolder.Seed.TurnIntoKudzu
                );
                tolerancesData = new PlantAnalyzerTolerancesData(
                    waterConsumption: plantHolder.Seed.WaterConsumption,
                    nutrientConsumption: plantHolder.Seed.NutrientConsumption,
                    toxinsTolerance: plantHolder.Seed.ToxinsTolerance,
                    pestTolerance: plantHolder.Seed.PestTolerance,
                    weedTolerance: plantHolder.Seed.WeedTolerance,
                    lowPressureTolerance: plantHolder.Seed.LowPressureTolerance,
                    highPressureTolerance: plantHolder.Seed.HighPressureTolerance,
                    idealHeat: plantHolder.Seed.IdealHeat,
                    heatTolerance: plantHolder.Seed.HeatTolerance,
                    idealLight: plantHolder.Seed.IdealLight,
                    lightTolerance: plantHolder.Seed.LightTolerance,
                    consumeGasses: [.. plantHolder.Seed.ConsumeGasses.Keys]
                );
                produceData = new PlantAnalyzerProduceData(
                    yield: plantHolder.Seed.ProductPrototypes.Count == 0 ? 0 : BotanySystem.CalculateTotalYield(plantHolder.Seed.Yield, plantHolder.YieldMod),
                    potency: plantHolder.Seed.Potency,
                    chemicals: [.. plantHolder.Seed.Chemicals.Keys],
                    produce: plantHolder.Seed.ProductPrototypes,
                    exudeGasses: [.. plantHolder.Seed.ExudeGasses.Keys],
                    seedless: plantHolder.Seed.Seedless
                );
            }
            trayData = new PlantAnalyzerTrayData(
                waterLevel: plantHolder.WaterLevel,
                nutritionLevel: plantHolder.NutritionLevel,
                toxins: plantHolder.Toxins,
                pestLevel: plantHolder.PestLevel,
                weedLevel: plantHolder.WeedLevel,
                chemicals: plantHolder.SoilSolution?.Comp.Solution.Contents.Select(r => r.Reagent.Prototype).ToList()
            );
        }

        return new PlantAnalyzerScannedUserMessage(
            GetNetEntity(target),
            scanMode,
            plantData,
            trayData,
            tolerancesData,
            produceData,
            analyzer.PrintReadyAt
        );
    }

    private void OnPrint(EntityUid uid, PlantAnalyzerComponent component, PlantAnalyzerPrintMessage args)
    {
        var user = args.Actor;

        if (Timing.CurTime < component.PrintReadyAt)
        {
            // This shouldn't occur due to the UI guarding against it, but
            // if it does, tell the user why nothing happened.
            PopupSystem.PopupEntity(Loc.GetString("forensic-scanner-printer-not-ready"), uid, user);
            return;
        }

        // Spawn a piece of paper.
        var printed = EntityManager.SpawnEntity(component.MachineOutput, Transform(uid).Coordinates);
        _handsSystem.PickupOrDrop(args.Actor, printed, checkActionBlocker: false);

        if (!TryComp<PaperComponent>(printed, out var paperComp))
        {
            Log.Error("Printed paper did not have PaperComponent.");
            return;
        }

        var data = GatherData(component);
        var missingData = Loc.GetString("plant-analyzer-printout-missing");

        var seedName = data.PlantData is not null ? Loc.GetString(data.PlantData.SeedDisplayName) : null;
        (string, object)[] parameters = [
            ("seedName", seedName ?? missingData),
            ("produce", data.ProduceData is not null ? PlantAnalyzerLocalizationHelper.ProduceToLocalizedStrings(data.ProduceData.Produce, _prototypeManager).Singular : missingData),
            ("producePlural", data.ProduceData is not null ? PlantAnalyzerLocalizationHelper.ProduceToLocalizedStrings(data.ProduceData.Produce, _prototypeManager).Plural : missingData),
            ("water", data.TolerancesData?.WaterConsumption.ToString(PlantAnalyzerLocalizationHelper.DP) ?? missingData),
            ("nutrients", data.TolerancesData?.NutrientConsumption.ToString(PlantAnalyzerLocalizationHelper.DP) ?? missingData),
            ("toxins", data.TolerancesData?.ToxinsTolerance.ToString(PlantAnalyzerLocalizationHelper.DP) ?? missingData),
            ("pests", data.TolerancesData?.PestTolerance.ToString(PlantAnalyzerLocalizationHelper.DP) ?? missingData),
            ("weeds", data.TolerancesData?.WeedTolerance.ToString(PlantAnalyzerLocalizationHelper.DP) ?? missingData),
            ("gasesIn", data.TolerancesData is not null ? PlantAnalyzerLocalizationHelper.GasesToLocalizedStrings(data.TolerancesData.ConsumeGasses, _prototypeManager) : missingData),
            ("kpa", data.TolerancesData?.IdealPressure.ToString(PlantAnalyzerLocalizationHelper.DP) ?? missingData),
            ("kpaTolerance", data.TolerancesData?.PressureTolerance.ToString(PlantAnalyzerLocalizationHelper.DP) ?? missingData),
            ("temp", data.TolerancesData?.IdealHeat.ToString(PlantAnalyzerLocalizationHelper.DP) ?? missingData),
            ("tempTolerance", data.TolerancesData?.HeatTolerance.ToString(PlantAnalyzerLocalizationHelper.DP) ?? missingData),
            ("lightLevel", data.TolerancesData?.IdealLight.ToString(PlantAnalyzerLocalizationHelper.DP) ?? missingData),
            ("lightTolerance", data.TolerancesData?.LightTolerance.ToString(PlantAnalyzerLocalizationHelper.DP) ?? missingData),
            ("yield", data.ProduceData?.Yield ?? -1),
            ("potency", data.ProduceData is not null ? data.ProduceData.Potency : missingData),
            ("potencyDesc", data.ProduceData is not null ? Loc.GetString(data.ProduceData.PotencyDesc) : missingData),
            ("chemicals", data.ProduceData is not null ? PlantAnalyzerLocalizationHelper.ChemicalsToLocalizedStrings(data.ProduceData.Chemicals, _prototypeManager) : missingData),
            ("chemCount", data.ProduceData?.Chemicals.Count.ToString(PlantAnalyzerLocalizationHelper.DP) ?? missingData),
            ("gasesOut", data.ProduceData is not null ? PlantAnalyzerLocalizationHelper.GasesToLocalizedStrings(data.ProduceData.ExudeGasses, _prototypeManager) : missingData),
            ("gasCount", data.ProduceData?.ExudeGasses.Count.ToString(PlantAnalyzerLocalizationHelper.DP) ?? missingData),
            ("endurance", data.PlantData?.Endurance.ToString(PlantAnalyzerLocalizationHelper.DP) ?? missingData),
            ("lifespan", data.PlantData?.Lifespan.ToString(PlantAnalyzerLocalizationHelper.DP) ?? missingData),
            ("seeds", data.ProduceData is not null ? PlantAnalyzerLocalizationHelper.BooleanToLocalizedStrings(data.ProduceData.Seedless ? true : false, _prototypeManager) : missingData),
            ("viable", data.PlantData is not null ? PlantAnalyzerLocalizationHelper.BooleanToLocalizedStrings(data.PlantData.Viable ? true : false, _prototypeManager) : missingData),
            ("kudzu", data.PlantData is not null ? PlantAnalyzerLocalizationHelper.BooleanToLocalizedStrings(data.PlantData.Kudzu ? true : false, _prototypeManager) : missingData),
            ("indent", "    "),
            ("nl", "\n")
        ];

        _paperSystem.SetContent((printed, paperComp), Loc.GetString($"plant-analyzer-printout", [.. parameters]));
        _labelSystem.Label(printed, seedName);
        Audio.PlayPvs(component.SoundPrint, uid, AudioParams.Default);

        component.PrintReadyAt = Timing.CurTime + component.PrintCooldown;
    }



    /// <inheritdoc/>
    protected override Enum GetUiKey()
    {
        return PlantAnalyzerUiKey.Key;
    }

    /// <inheritdoc/>
    protected override bool ScanTargetPopupMessage(Entity<PlantAnalyzerComponent> uid, AfterInteractEvent args, [NotNullWhen(true)] out string? message)
    {
        message = null;
        return false;
    }

    /// <inheritdoc/>
    protected override bool ValidScanTarget(EntityUid? target)
    {
        return HasComp<PlantHolderComponent>(target);
    }

    public override void BeginAnalyzingEntity(Entity<PlantAnalyzerComponent> analyzer, EntityUid target, EntityUid? part = null)
    {
        //Link the analyzer to the scanned entity
        analyzer.Comp.ScannedEntity = target;

        Toggle.TryActivate(analyzer.Owner);

        UpdateScannedUser(analyzer, target, true);
    }
}
