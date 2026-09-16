namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>
/// The trainee opened the anchor's own window - a microwave panel, a sheet of paper held in hand.
/// Plain interaction is not enough here, because picking a thing up already counts as one.
/// </summary>
public sealed partial class UiOpenedAnchorCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;
}
