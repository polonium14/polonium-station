namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>Something is growing in the tray behind this anchor.</summary>
public sealed partial class PlantTrayPlantedCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;
}
