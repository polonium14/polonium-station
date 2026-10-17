namespace Content.Shared._Polonium.Tutorial.Actions;

/// <summary>
/// Every shove on the anchor lands and puts it straight down. Take it off again afterwards,
/// the same component also makes cuffing instant.
/// </summary>
public sealed partial class SetDisarmProneAction : TutorialAction
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;

    [DataField]
    public bool Prone = true;
}
