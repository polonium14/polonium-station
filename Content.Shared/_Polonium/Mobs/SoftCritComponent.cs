using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Mobs;

/// <summary>
/// Splits the critical state in two. Below <see cref="HardCritThreshold"/> the mob is in soft crit:
/// it stays conscious, crawls and whispers, but suffocates like hard crit and cannot attack, use items or the radio.
/// From the threshold on it is in hard crit, the usual critical state.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SoftCritSystem), Other = AccessPermissions.Read)]
public sealed partial class SoftCritComponent : Component
{
    /// <summary>
    /// Total damage from which a critical mob is in hard crit.
    /// </summary>
    [DataField]
    public FixedPoint2 HardCritThreshold = 130;

    /// <summary>
    /// Once in hard crit, the mob goes back to soft crit only this far below <see cref="HardCritThreshold"/>,
    /// so healing around the threshold does not stand it up and lay it down again and again.
    /// </summary>
    [DataField]
    public FixedPoint2 RecoveryMargin = 5;

    /// <summary>
    /// Movement speed multiplier in soft crit, crawling or walking.
    /// </summary>
    [DataField]
    public float SpeedModifier = 0.4f;

    /// <summary>
    /// Whether the mob was in soft crit at the last check. Only used to react when that changes.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Active;

    /// <summary>
    /// Whether the mob is in hard crit, which moves the way back to soft crit down by <see cref="RecoveryMargin"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool HardCrit;
}
