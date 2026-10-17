namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>
/// View turned since the step began. Right is what the rotate-right key does, one press is a
/// quarter turn.
/// </summary>
public sealed partial class CameraRotatedCondition : TutorialCondition
{
    [DataField]
    public float Degrees = 80f;

    [DataField]
    public float MaxDegrees = 180f;

    [DataField]
    public TutorialRotation Direction = TutorialRotation.Any;
}

public enum TutorialRotation : byte
{
    Any,
    Right,
    Left,
}
