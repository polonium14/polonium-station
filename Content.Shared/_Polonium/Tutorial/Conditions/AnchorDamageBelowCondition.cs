using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>
/// One damage type on the anchor is down to <see cref="Max"/> or less, so a patient can be treated
/// one injury at a time instead of all or nothing. Without a type it is all of the damage together.
/// </summary>
public sealed partial class AnchorDamageBelowCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;

    [DataField]
    public ProtoId<DamageTypePrototype>? DamageType;

    [DataField]
    public float Max;
}
