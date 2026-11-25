using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Wounds;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Shitmed.PartStatus;

public sealed class PartStatus(
    TargetBodyPart part,
    string partName,
    WoundableSeverity partSeverity,
    Dictionary<ProtoId<DamageTypePrototype>, PartWound> wounds,
    BoneSeverity boneSeverity,
    bool bleeding)
{
    public TargetBodyPart Part = part;

    public string PartName = partName;

    public WoundableSeverity PartSeverity = partSeverity;

    public Dictionary<ProtoId<DamageTypePrototype>, PartWound> Wounds = wounds;

    public BoneSeverity BoneSeverity = boneSeverity;

    public bool Bleeding = bleeding;
}

/// <summary>
/// The worst wound of one damage type on a body part, with the group that colours it.
/// </summary>
public readonly record struct PartWound(ProtoId<DamageGroupPrototype> Group, WoundSeverity Severity);
