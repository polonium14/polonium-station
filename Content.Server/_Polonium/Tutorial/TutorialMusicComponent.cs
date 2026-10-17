namespace Content.Server._Polonium.Tutorial;

/// <summary>The looping track an anchor is playing for a tutorial room.</summary>
[RegisterComponent]
public sealed partial class TutorialMusicComponent : Component
{
    [ViewVariables]
    public EntityUid? Stream;
}
