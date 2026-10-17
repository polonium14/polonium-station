namespace Content.Shared._Polonium.Tutorial.Actions;

/// <summary>
/// From here on the trainee is expected to keep insulated gloves on. Taking them off gets a
/// safety warning, and a shock gets an "I told you".
/// </summary>
public sealed partial class RequireGlovesAction : TutorialAction
{
    [DataField]
    public bool Required = true;
}
