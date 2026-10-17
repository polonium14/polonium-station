namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>The anchor sits directly inside the other anchor's container, e.g. a body shut in the morgue.</summary>
public sealed partial class AnchorInsideAnchorCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;

    [DataField(required: true)]
    public string ContainerAnchorId = string.Empty;
}
