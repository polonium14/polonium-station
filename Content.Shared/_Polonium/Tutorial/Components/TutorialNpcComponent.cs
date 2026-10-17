namespace Content.Shared._Polonium.Tutorial.Components;

[RegisterComponent]
public sealed partial class TutorialNpcComponent : Component
{
    [DataField]
    public bool PreventDeath = true;
}
