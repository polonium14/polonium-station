using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Wounds;
using Content.Shared.Eui;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Body;

[Serializable, NetSerializable]
public enum BodyDebugOrganKind : byte
{
    Alive,
    Dead,
    Missing,
}

/// <summary>
/// What a slider of the body debug window changes.
/// </summary>
[Serializable, NetSerializable]
public enum BodyDebugSlider : byte
{
    Organ,
    Limb,
    Bone,
    Blood,
    Temperature,
    Saturation,
}

/// <summary>
/// One-click actions of the body debug window on an organ or a body part.
/// </summary>
[Serializable, NetSerializable]
public enum BodyDebugAction : byte
{
    KillOrgan,
    ReviveOrgan,
    RemoveOrgan,
    HealLimb,
    StopBleeding,
    Amputate,
    ToggleLobotomy,
    EjectCavity,
    ResetSurgery,
    RemoveTourniquet,
    ToggleCirculation,
}

/// <summary>
/// One organ row of the body debug window. Everything is sent by value, because the admin can be out of PVS range.
/// </summary>
/// <param name="Organ">Null for a missing organ.</param>
/// <param name="HealthyFrom">Lowest integrity at which the organ is still healthy.</param>
/// <param name="Floor">Integrity never drops below this value.</param>
/// <param name="Protected">True for organs that can only have their integrity changed.</param>
/// <param name="FailureStage">Current failure stage, 0 if none.</param>
/// <param name="FailureStages">Number of failure stages, 0 for organs that cannot fail.</param>
[Serializable, NetSerializable]
public readonly record struct BodyDebugOrgan(
    NetEntity? Organ,
    string Category,
    string Name,
    BodyDebugOrganKind Kind,
    FixedPoint2 Integrity,
    FixedPoint2 Cap,
    FixedPoint2 HealthyFrom,
    FixedPoint2 Floor,
    OrganSeverity Severity,
    List<(string Name, FixedPoint2 Value)> Modifiers,
    bool Protected,
    int FailureStage = 0,
    int FailureStages = 0);

/// <summary>
/// One body part row of the body debug window.
/// </summary>
/// <param name="Limb">Null for a missing body part.</param>
/// <param name="BoneCap">Zero if the part has no bone.</param>
[Serializable, NetSerializable]
public readonly record struct BodyDebugLimb(
    NetEntity? Limb,
    string Category,
    string Name,
    FixedPoint2 Integrity,
    FixedPoint2 Cap,
    WoundableSeverity Severity,
    FixedPoint2 Bleeds,
    FixedPoint2 BoneIntegrity,
    FixedPoint2 BoneCap,
    BoneSeverity BoneSeverity,
    List<(string Name, FixedPoint2 Severity)> Wounds,
    bool CanAmputate,
    bool Lobotomized = false,
    string? CavityItem = null,
    bool UnfinishedSurgery = false,
    bool Tourniquet = false);

/// <summary>
/// Whole-body readings of the body debug window, each null when the body does not have it.
/// </summary>
/// <param name="CirculationArrested">The window stopped the circulation itself, with a working heart.</param>
[Serializable, NetSerializable]
public readonly record struct BodyDebugVitals(
    FixedPoint2? BloodVolume,
    FixedPoint2 NormalBloodVolume,
    float? Temperature,
    float? Saturation,
    float MinSaturation,
    float MaxSaturation,
    bool CirculationStopped,
    bool CirculationArrested);

/// <param name="WoundTypes">Damage types the admin can wound a body part with.</param>
/// <param name="Error">Localization key of the reason the last action failed.</param>
[Serializable, NetSerializable]
public sealed class BodyDebugEuiState(
    NetEntity body,
    string targetName,
    List<BodyDebugOrgan> organs,
    List<BodyDebugLimb> limbs,
    List<string> woundTypes,
    BodyDebugVitals vitals,
    string? error) : EuiStateBase
{
    /// <summary>
    /// The body itself, the target of the whole-body sliders and actions.
    /// </summary>
    public readonly NetEntity Body = body;

    public readonly BodyDebugVitals Vitals = vitals;
    public readonly string TargetName = targetName;
    public readonly List<BodyDebugOrgan> Organs = organs;
    public readonly List<BodyDebugLimb> Limbs = limbs;
    public readonly List<string> WoundTypes = woundTypes;
    public readonly string? Error = error;
}

public static class BodyDebugEuiMsg
{
    /// <summary>
    /// Sets integrities, or the body's blood, temperature and saturation, to the given values.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class Apply(List<(NetEntity Target, BodyDebugSlider Slider, FixedPoint2 Integrity)> changes) : EuiMessageBase
    {
        public readonly List<(NetEntity Target, BodyDebugSlider Slider, FixedPoint2 Integrity)> Changes = changes;
    }

    [Serializable, NetSerializable]
    public sealed class Act(BodyDebugAction action, NetEntity target) : EuiMessageBase
    {
        public readonly BodyDebugAction Action = action;
        public readonly NetEntity Target = target;
    }

    /// <summary>
    /// Puts a missing organ or body part back.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class Insert(string category) : EuiMessageBase
    {
        public readonly string Category = category;
    }

    [Serializable, NetSerializable]
    public sealed class AddWound(NetEntity limb, string damageType, int amount) : EuiMessageBase
    {
        public readonly NetEntity Limb = limb;
        public readonly string DamageType = damageType;
        public readonly int Amount = amount;
    }

    [Serializable, NetSerializable]
    public sealed class SetFailureStage(NetEntity organ, int stage) : EuiMessageBase
    {
        public readonly NetEntity Organ = organ;
        public readonly int Stage = stage;
    }

    [Serializable, NetSerializable]
    public sealed class Refresh : EuiMessageBase;
}
