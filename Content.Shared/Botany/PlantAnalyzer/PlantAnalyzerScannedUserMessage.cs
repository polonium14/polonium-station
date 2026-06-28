// SPDX-FileCopyrightText: 2025 Mehnix <56132549+Mehnix@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Atmos;
using Robust.Shared.Serialization;

namespace Content.Shared.Botany.PlantAnalyzer;

[Serializable, NetSerializable]
public sealed class PlantAnalyzerScannedUserMessage(NetEntity? targetEntity, bool? scanMode, PlantAnalyzerPlantData? plantData, PlantAnalyzerTrayData? trayData, PlantAnalyzerTolerancesData? tolerancesData, PlantAnalyzerProduceData? produceData, TimeSpan? printReadyAt) : BoundUserInterfaceMessage
{
    public readonly NetEntity? TargetEntity = targetEntity;
    public bool? ScanMode = scanMode;
    public PlantAnalyzerPlantData? PlantData = plantData;
    public PlantAnalyzerTrayData? TrayData = trayData;
    public PlantAnalyzerTolerancesData? TolerancesData = tolerancesData;
    public PlantAnalyzerProduceData? ProduceData = produceData;
    public readonly TimeSpan? PrintReadyAt = printReadyAt;
}

/// <summary>
/// Everything that is kept independed of a given plant/seed.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlantAnalyzerTrayData(float waterLevel, float nutritionLevel, float toxins, float pestLevel, float weedLevel, List<string>? chemicals)
{
    public float WaterLevel = waterLevel;
    public float NutritionLevel = nutritionLevel;
    public float Toxins = toxins;
    public float PestLevel = pestLevel;
    public float WeedLevel = weedLevel;
    public List<string>? Chemicals = chemicals;
}


/// <summary>
/// All the information to keep the plant alive.
/// Which is most of the "Tolerances" region plus the gases it may need.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlantAnalyzerTolerancesData(float nutrientConsumption, float waterConsumption, float idealHeat, float heatTolerance, float idealLight, float lightTolerance, float toxinsTolerance, float lowPressureTolerance, float highPressureTolerance, float pestTolerance, float weedTolerance, List<Gas> consumeGasses)
{
    public float WaterConsumption = waterConsumption;
    public float NutrientConsumption = nutrientConsumption;
    public float ToxinsTolerance = toxinsTolerance;
    public float PestTolerance = pestTolerance;
    public float WeedTolerance = weedTolerance;
    public float IdealPressure = (lowPressureTolerance + highPressureTolerance) / 2;
    public float PressureTolerance = (lowPressureTolerance + highPressureTolerance) / 2 - lowPressureTolerance;
    public float IdealHeat = idealHeat;
    public float HeatTolerance = heatTolerance;
    public float IdealLight = idealLight;
    public float LightTolerance = lightTolerance;
    public List<Gas> ConsumeGasses = consumeGasses;
}

/// <summary>
/// Information about the plant inside the tray.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlantAnalyzerPlantData(string seedDisplayName, float health, float endurance, float age, float lifespan, bool dead, bool viable, bool mutating, bool kudzu)
{
    public string SeedDisplayName = seedDisplayName;
    public float Health = health;
    public float Endurance = endurance;
    public float Age = age;
    public float Lifespan = lifespan;
    public bool Dead = dead;
    public bool Viable = viable;
    public bool Mutating = mutating;
    public bool Kudzu = kudzu;
}

/// <summary>
/// Information about the output of a plant (produce and gas).
/// </summary>
[Serializable, NetSerializable]
public sealed class PlantAnalyzerProduceData(int yield, float potency, List<string> chemicals, List<string> produce, List<Gas> exudeGasses, bool seedless)
{
    public int Yield = yield;
    public float Potency = potency;
    public string PotencyDesc = ObscurePotency(potency);
    public List<string> Chemicals = chemicals;
    public List<string> Produce = produce;
    public List<Gas> ExudeGasses = exudeGasses;
    public bool Seedless = seedless;

    private static string ObscurePotency(float potency)
    {
        var potencyFtl = "plant-analyzer-potency-";
        if (potency <= 10)
            potencyFtl += "tiny";
        else if (potency < 20)
            potencyFtl += "small";
        else if (potency < 30)
            potencyFtl += "medium";
        else if (potency < 40)
            potencyFtl += "large";
        else if (potency <= 50) //biggest you can get with robust harvest alone.
            potencyFtl += "huge";
        else if (potency < 65)
            potencyFtl += "gigantic";
        else if (potency < 80)
            potencyFtl += "ludicrous";
        else if (potency < 99)
            potencyFtl += "immeasurable";
        else
            potencyFtl += "perfect"; //you've earned this, king.

        return potencyFtl;
    }
}

[Serializable, NetSerializable]
public sealed class PlantAnalyzerPrintMessage : BoundUserInterfaceMessage
{
}
