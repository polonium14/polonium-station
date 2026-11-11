using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Whether admins get an alert when the server refuses where a predicting client said it fired from.
    /// Lag can also cause it, so it only hints at cheating.
    /// </summary>
    public static readonly CVarDef<bool> GunOriginRejectionAlerts =
        CVarDef.Create("gun.origin_rejection_alerts", false, CVar.SERVERONLY);

    /// <summary>
    /// Seconds before the same player can trigger another admin alert about a refused shot origin.
    /// </summary>
    public static readonly CVarDef<float> GunOriginRejectionAlertCooldown =
        CVarDef.Create("gun.origin_rejection_alert_cooldown", 8f, CVar.SERVERONLY);

    /// <summary>
    /// Whether moving, running and sprinting widen the spread of guns with GunMovementSpreadComponent.
    /// </summary>
    public static readonly CVarDef<bool> GunMovementSpread =
        CVarDef.Create("gun.movement_spread", false, CVar.SERVER | CVar.REPLICATED);
}
