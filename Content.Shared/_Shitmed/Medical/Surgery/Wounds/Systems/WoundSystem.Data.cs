using Content.Shared.FixedPoint;

namespace Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;

public sealed partial class WoundSystem
{
    /// <summary>
    /// Shared per-wound severity thresholds (0-100 scale), scaled per-woundable by
    /// (IntegrityCap / 100) in <see cref="CheckSeverityThresholds"/>.
    /// </summary>
    public static readonly Dictionary<WoundSeverity, FixedPoint2> WoundThresholds = new()
    {
        { WoundSeverity.Loss, 100 },
        { WoundSeverity.Critical, 80 },
        { WoundSeverity.Severe, 50 },
        { WoundSeverity.Moderate, 25 },
        { WoundSeverity.Minor, 1 },
        { WoundSeverity.Healed, 0 },
    };
}
