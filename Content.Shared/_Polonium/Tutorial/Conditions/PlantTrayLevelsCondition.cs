namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>
/// The water and nutrient gauges a tray shows when you look at it. Reading the gauges instead of
/// the soil is what lets a step wait for "it soaked in", since pouring only fills the soil and the
/// tray hands it over to the gauges a unit at a time.
/// </summary>
public sealed partial class PlantTrayLevelsCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;

    [DataField]
    public float Water;

    [DataField]
    public float Nutrition;
}
