using Content.Shared._Shitmed.Medical.HealthAnalyzer;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Wounds;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body;
using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Medical.HealthAnalyzer;

/// <summary>
/// Everything the health analyzer shows about a patient, sent whole once a second. The window draws only from this,
/// so it works the same for any viewer, whatever of the patient they can see. A null target means no patient.
/// </summary>
[Serializable, NetSerializable]
public sealed class HealthAnalyzerScanMessage : BoundUserInterfaceMessage
{
    public NetEntity? Target;

    /// <summary>
    /// Whether the patient is still in range. Null for scanners that are always connected.
    /// </summary>
    public bool? ScanActive;

    public float Temperature = float.NaN;
    public float ColdDamageThreshold = float.NaN;
    public float HeatDamageThreshold = float.NaN;

    /// <summary>
    /// Blood as a fraction of the normal amount, and the fraction below which blood loss starts to hurt.
    /// </summary>
    public float BloodLevel = float.NaN;
    public float BloodlossThreshold = float.NaN;

    /// <summary>
    /// Blood in units, and how much a healthy body of this species has.
    /// </summary>
    public FixedPoint2 BloodVolume;
    public FixedPoint2 NormalBloodVolume;

    /// <summary>
    /// Pulse oximeter reading in percent; null when the patient does not breathe or is dead.
    /// </summary>
    public float? SpO2;

    /// <summary>
    /// Whole-body damage by type, the same total the critical and dead thresholds are counted against.
    /// </summary>
    public Dictionary<string, FixedPoint2> Damage = new();
    public FixedPoint2? CritThreshold;
    public FixedPoint2? DeadThreshold;

    public Dictionary<TargetBodyPart, AnalyzerLimbData> Limbs = new();
    public bool SystemicBleeding;

    /// <summary>
    /// Every organ the body has or should have, missing ones included.
    /// </summary>
    public List<AnalyzerOrganData> Organs = new();

    public bool CirculationStopped;

    /// <summary>
    /// Why a defibrillator will not bring the patient back, if it will not.
    /// </summary>
    public LocId? Unrevivable;

    /// <summary>
    /// What is dissolved in the blood, without the blood itself.
    /// </summary>
    public Solution? BloodChemicals;

    /// <summary>
    /// The patient's other solutions (stomach, metabolites and so on).
    /// </summary>
    public List<NamedSolution> Solutions = new();
}

[Serializable, NetSerializable]
public struct AnalyzerLimbData
{
    public WoundableSeverity Severity;
    public bool Bleeding;
    public bool Tourniquet;
    public bool UnfinishedSurgery;
    public BoneSeverity? Bone;
    public Dictionary<string, FixedPoint2> Damage;

    /// <summary>
    /// How much damage the limb can hold before the rest goes to its parent; zero when unknown.
    /// </summary>
    public FixedPoint2 IntegrityCap;
}

[Serializable, NetSerializable]
public struct AnalyzerOrganData
{
    public ProtoId<OrganCategoryPrototype> Category;
    public FixedPoint2 Integrity;
    public FixedPoint2 IntegrityCap;
    public OrganSeverity Severity;
    public int FailureStage;
    public bool Dead;
    public bool Missing;
}
