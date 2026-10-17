namespace Content.Shared._Polonium.Tutorial.Actions;

/// <summary>Turns the trainee to face a grid direction, for when they are meant to be looking at something.</summary>
public sealed partial class FaceDirectionAction : TutorialAction
{
    [DataField]
    public Direction Direction = Direction.South;
}
