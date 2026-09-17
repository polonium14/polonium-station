namespace Content.Shared._Polonium.Tutorial.Conditions;

/// <summary>
/// The view is back where the reset key puts it, within a few degrees.
/// </summary>
public sealed partial class CameraAlignedCondition : TutorialCondition
{
    [DataField]
    public float Degrees = 10f;
}
