namespace Content.Server._Polonium.Tutorial;

/// <summary>
/// Put on a half built machine so examining it reaches the tutorial. Construction and machine frames
/// already listen to examine for their own components, and a second listener there would crash startup.
/// </summary>
[RegisterComponent]
public sealed partial class TutorialExamineProbeComponent : Component
{
    [ViewVariables]
    public string? Flag;
}
