namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>The anchor has no shots left - a battery weapon fired flat, a magazine shot dry.</summary>
public sealed partial class AnchorAmmoEmptyCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;
}
