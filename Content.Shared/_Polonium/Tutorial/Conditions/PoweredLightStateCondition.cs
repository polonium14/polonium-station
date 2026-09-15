namespace Content.Shared._Polonium.Tutorial.Conditions;

public sealed partial class PoweredLightStateCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;

    [DataField]
    public bool On;
}
