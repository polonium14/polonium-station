// SPDX-FileCopyrightText: 2025 Mehnix <56132549+Mehnix@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.BaseAnalyzer;
using Content.Server.Botany.Components;
using Content.Server.Popups;
using Content.Shared.Atmos;
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
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Systems;
using Content.Shared.Botany.Traits.Components;


namespace Content.Server.Botany.Systems;

public sealed partial class PlantAnalyzerSystem : BaseAnalyzerSystem<PlantAnalyzerComponent, PlantAnalyzerDoAfterEvent>
{
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private SharedHandsSystem _handsSystem = default!;
    [Dependency] private PaperSystem _paperSystem = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private LabelSystem _labelSystem = default!;
    [Dependency] private PlantTraySystem _plantTray = default!;

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

        if (_entityManager.TryGetComponent<PlantTrayComponent>(target, out var tray))
        {
            trayData = new PlantAnalyzerTrayData(
                waterLevel: tray.WaterLevel,
                nutritionLevel: tray.NutritionLevel,
                toxins: tray.ToxinLevel,
                pestLevel: tray.PestLevel,
                weedLevel: tray.WeedLevel,
                chemicals: tray.SoilSolution?.Comp.Solution.Contents.Select(r => r.Reagent.Prototype.Id).ToList()
            );

            // The plant is now a separate entity parented to the tray; its data is split across several components.
            if (_plantTray.TryGetPlant((target.Value, tray), out var plantUid)
                && TryComp<PlantComponent>(plantUid, out var plant)
                && TryComp<PlantHolderComponent>(plantUid, out var holder)
                && TryComp<PlantDataComponent>(plantUid, out var plantMeta))
            {
                plantData = new PlantAnalyzerPlantData(
                    seedDisplayName: plantMeta.Name,
                    health: holder.Health,
                    endurance: plant.Endurance,
                    age: holder.Age,
                    lifespan: plant.Lifespan,
                    dead: holder.Dead,
                    viable: true, // TODO: no new-model equivalent; a live plant is treated as viable
                    mutating: holder.MutationLevel > 0f,
                    kudzu: HasComp<PlantTraitKudzuComponent>(plantUid)
                );

                var waterConsumption = 0f;
                var nutrientConsumption = 0f;
                if (TryComp<PlantGrowthComponent>(plantUid, out var growth))
                {
                    waterConsumption = growth.WaterConsumption;
                    nutrientConsumption = growth.NutrientConsumption;
                }

                var toxinsTolerance = TryComp<PlantToxinsComponent>(plantUid, out var plantToxins)
                    ? plantToxins.ToxinsTolerance
                    : 0f;

                var pestTolerance = 0f;
                var weedTolerance = 0f;
                if (TryComp<PlantWeedPestComponent>(plantUid, out var weedPest))
                {
                    pestTolerance = weedPest.PestTolerance;
                    weedTolerance = weedPest.WeedTolerance;
                }

                var idealHeat = 0f;
                var heatTolerance = 0f;
                var lowPressureTolerance = 0f;
                var highPressureTolerance = 0f;
                if (TryComp<PlantAtmosphericComponent>(plantUid, out var atmos))
                {
                    idealHeat = (atmos.LowHeatTolerance + atmos.HighHeatTolerance) / 2f;
                    heatTolerance = (atmos.HighHeatTolerance - atmos.LowHeatTolerance) / 2f;
                    lowPressureTolerance = atmos.LowPressureTolerance;
                    highPressureTolerance = atmos.HighPressureTolerance;
                }

                List<Gas> consumeGasses = new();
                List<Gas> exudeGasses = new();
                if (TryComp<PlantConsumeExudeGasComponent>(plantUid, out var gas))
                {
                    consumeGasses = [.. gas.ConsumeGasses.Keys];
                    exudeGasses = [.. gas.ExudeGasses.Keys];
                }

                tolerancesData = new PlantAnalyzerTolerancesData(
                    waterConsumption: waterConsumption,
                    nutrientConsumption: nutrientConsumption,
                    toxinsTolerance: toxinsTolerance,
                    pestTolerance: pestTolerance,
                    weedTolerance: weedTolerance,
                    lowPressureTolerance: lowPressureTolerance,
                    highPressureTolerance: highPressureTolerance,
                    idealHeat: idealHeat,
                    heatTolerance: heatTolerance,
                    idealLight: 0f, // TODO: no new-model equivalent
                    lightTolerance: 0f, // TODO: no new-model equivalent
                    consumeGasses: consumeGasses
                );

                List<string> chemicals = TryComp<PlantChemicalsComponent>(plantUid, out var chem)
                    ? chem.Chemicals.Keys.Select(x => x.Id).ToList()
                    : new List<string>();

                produceData = new PlantAnalyzerProduceData(
                    yield: plantMeta.ProductPrototypes.Count == 0
                        ? 0
                        : (holder.YieldMod < 0 ? plant.Yield : plant.Yield * holder.YieldMod),
                    potency: plant.Potency,
                    chemicals: chemicals,
                    produce: plantMeta.ProductPrototypes.Select(x => x.Id).ToList(),
                    exudeGasses: exudeGasses,
                    seedless: HasComp<PlantTraitSeedlessComponent>(plantUid)
                );
            }
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
        var printed = Spawn(component.MachineOutput, Transform(uid).Coordinates);
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
        return HasComp<PlantTrayComponent>(target);
    }

    public override void BeginAnalyzingEntity(Entity<PlantAnalyzerComponent> analyzer, EntityUid target, EntityUid? part = null)
    {
        //Link the analyzer to the scanned entity
        analyzer.Comp.ScannedEntity = target;

        Toggle.TryActivate(analyzer.Owner);

        UpdateScannedUser(analyzer, target, true);
    }
}
