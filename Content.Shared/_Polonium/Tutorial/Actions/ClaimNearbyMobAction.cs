namespace Content.Shared._Polonium.Tutorial.Actions;

public sealed partial class ClaimNearbyMobAction : TutorialAction
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;

    [DataField]
    public string? AssignAnchorId;

    [DataField]
    public float Range = 12f;

    [DataField]
    public bool PreventDeath = true;

    [DataField]
    public bool MarkDeadPatient;

    [DataField]
    public string? PatientKind;

    [DataField]
    public string? PatientDamageType;
}
