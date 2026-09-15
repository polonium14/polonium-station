namespace Content.Shared._Polonium.Tutorial.Actions;

public sealed partial class ApplyDamageAction : TutorialAction
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;

    [DataField(required: true)]
    public string DamageType = string.Empty;

    [DataField]
    public float Amount = 10f;
}
