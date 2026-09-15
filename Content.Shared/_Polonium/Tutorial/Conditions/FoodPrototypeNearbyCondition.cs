namespace Content.Shared._Polonium.Tutorial.Conditions;

public sealed partial class FoodPrototypeNearbyCondition : TutorialCondition
{
    [DataField(required: true)]
    public string Prototype = string.Empty;

    [DataField]
    public float Range = 8f;
}
