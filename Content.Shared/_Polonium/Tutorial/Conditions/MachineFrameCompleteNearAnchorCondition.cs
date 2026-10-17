namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>A machine frame at the anchor holds its board and every part the board asks for.</summary>
public sealed partial class MachineFrameCompleteNearAnchorCondition : TutorialCondition
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;

    [DataField]
    public float Range = 0.5f;
}
