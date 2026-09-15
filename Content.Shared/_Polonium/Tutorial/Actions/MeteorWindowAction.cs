namespace Content.Shared._Polonium.Tutorial.Actions;

public sealed partial class MeteorWindowAction : TutorialAction
{
    [DataField(required: true)]
    public string WindowAnchor = string.Empty;

    [DataField]
    public int Countdown = 5;

    [DataField]
    public float Damage = 400f;
}
