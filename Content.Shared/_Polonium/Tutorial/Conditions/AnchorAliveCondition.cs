namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>The mob on this anchor is breathing again. Used to confirm a revival actually worked.</summary>
public sealed partial class AnchorAliveCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;
}
