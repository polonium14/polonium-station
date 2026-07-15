using Content.Shared.Body;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;

/// <summary>
/// A single induced trauma (bone damage, organ damage, veins/nerve damage, dismemberment).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TraumaComponent : Component
{
    /// <summary>
    /// Self-explanatory. Can be null if the organ or bone, etc; got delimbed but still exists.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public EntityUid? HoldingWoundable;

    /// <summary>
    /// For OrganDamage - the organ.
    /// For BoneDamage - the bone.
    /// For VeinsDamage and NerveDamage - the woundable.
    /// For Dismemberment - the parent woundable, of the woundable that got delimbed.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public EntityUid? TraumaTarget;

    /// <summary>
    /// Purely exists for delimb traumas, to know which limb-organ category was lost.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public ProtoId<OrganCategoryPrototype>? TargetCategory;

    /// <summary>
    /// The severity the wound had when trauma got induced; Gets updated to the new one if the trauma gets worsened by the same wound.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public FixedPoint2 TraumaSeverity;

    [DataField, ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public TraumaType TraumaType;
}
