namespace Content.Shared._Polonium.Tutorial.Components;

/// <summary>
/// Lets a hydroponics tray drink what was poured into it while it is still empty. A stock tray only
/// moves soil into its water and nutrient gauges once something is growing, so a trainee who waters
/// a bare tray watches both numbers sit at zero and both warning lights stay lit.
/// </summary>
[RegisterComponent]
public sealed partial class TutorialSoilComponent : Component;
