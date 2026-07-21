using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared._Shitmed.Medical.Surgery.Wounds;
using Content.Shared._Shitmed.Targeting;

namespace Content.Server._Shitmed.PartStatus;

public sealed class PartStatus(
    TargetBodyPart part,
    string partName,
    WoundableSeverity partSeverity,
    Dictionary<string, WoundSeverity> damageSeverities,
    BoneSeverity boneSeverity,
    bool bleeding)
{
    public TargetBodyPart Part = part;

    public string PartName = partName;

    public WoundableSeverity PartSeverity = partSeverity;

    public Dictionary<string, WoundSeverity> DamageSeverities = damageSeverities;

    public BoneSeverity BoneSeverity = boneSeverity;

    public bool Bleeding = bleeding;
}
