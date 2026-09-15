namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>Anchor holds no reagent any more, or is gone entirely. Drinking and spilling both count.</summary>
public sealed partial class AnchorEmptyOrGoneCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;
}
