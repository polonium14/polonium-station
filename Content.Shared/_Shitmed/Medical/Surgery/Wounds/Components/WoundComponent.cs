using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;

/// <summary>
/// A single wound, spawned as its own entity and inserted into the holding woundable's
/// Wounds container.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WoundComponent : Component
{
    /// <summary>
    /// The woundable (limb-organ) entity this wound was applied to.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public EntityUid HoldingWoundable;

    /// <summary>
    /// The integrity damage this wound contributes to its woundable.
    /// </summary>
    public FixedPoint2 WoundIntegrityDamage => WoundSeverityPoint;

    /// <summary>
    /// Raw severity accumulator. Drives <see cref="WoundSeverity"/> via WoundSystem.WoundThresholds.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public FixedPoint2 WoundSeverityPoint;

    /// <summary>
    /// External/Internal.
    /// </summary>
    [DataField]
    public WoundType WoundType = WoundType.External;

    /// <summary>
    /// Damage group this wound is associated with, for display/grouping purposes. Assigned
    /// imperatively server-side by WoundSystem.Queries.cs's TryCreateWound (not baked into any
    /// wound prototype's own YAML), so unlike DamageType below it needs AutoNetworkedField to
    /// ever reach the client at all - without it, every client-side damageGroup-filtered query
    /// (e.g. WoundableVisualsSystem's per-limb damage overlay severity) silently saw every
    /// wound's DamageGroup as null and filtered all of them out.
    /// </summary>
    [DataField]
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public ProtoId<DamageGroupPrototype>? DamageGroup;

    /// <summary>
    /// Damage type this wound was induced by.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<DamageTypePrototype> DamageType;

    /// <summary>
    /// Scar wound prototype spawned in this wound's place once healed, if any.
    /// </summary>
    [DataField]
    public EntProtoId? ScarWound;

    /// <summary>
    /// Whether this wound is itself a scar (excluded from integrity-damage sums and
    /// from being matched/continued by new damage of the same type).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsScar;

    /// <summary>
    /// Current severity bucket, derived from <see cref="WoundSeverityPoint"/>.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public WoundSeverity WoundSeverity = WoundSeverity.Minor;

    [DataField]
    public WoundVisibility WoundVisibility = WoundVisibility.Always;

    [DataField]
    public bool CanBeHealed = true;

    /// <summary>
    /// Severity threshold at which this wound triggers a trauma infliction, once Traumas lands.
    /// </summary>
    [DataField]
    public WoundSeverity? MangleSeverity;

    [DataField]
    public string TextString = "wound";

    [DataField]
    public float SelfHealMultiplier = 1.0f;
}
