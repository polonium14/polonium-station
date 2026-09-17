namespace Content.Shared._Polonium.Tutorial.Conditions;

public sealed partial class AnchorsNearCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;

    [DataField(required: true)]
    public string NearAnchorId = string.Empty;

    [DataField]
    public float Range = 2.5f;

    /// <summary>True once the two are further apart than Range instead.</summary>
    [DataField]
    public bool Away;
}
