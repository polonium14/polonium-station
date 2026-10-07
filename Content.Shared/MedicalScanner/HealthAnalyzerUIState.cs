using Content.Shared._Shitmed.Targeting;
using Robust.Shared.Serialization;

namespace Content.Shared.MedicalScanner;

/// <summary>
/// Contains the current state of a health analyzer control. Used for the health analyzer and cryo pod.
/// </summary>
[Serializable, NetSerializable]
public sealed class HealthAnalyzerUiState : BoundUserInterfaceState
{
    public readonly NetEntity? TargetEntity;
    public float Temperature;
    public float BloodLevel;
    public bool ScanMode;
    public bool Bleeding;
    public bool Unrevivable;

    public Dictionary<TargetBodyPart, string>? PartStatuses;

    public HealthAnalyzerUiState() {}

    public HealthAnalyzerUiState(NetEntity? targetEntity, float temperature, float bloodLevel, bool scanMode, bool bleeding, bool unrevivable, Dictionary<TargetBodyPart, string>? partStatuses = null) // Polonium: Shitmed part statuses
    {
        TargetEntity = targetEntity;
        Temperature = temperature;
        BloodLevel = bloodLevel;
        ScanMode = scanMode;
        Bleeding = bleeding;
        Unrevivable = unrevivable;
        PartStatuses = partStatuses;
    }
}
