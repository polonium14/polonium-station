using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Tutorial.Components;

/// <summary>
/// Trainee cannot walk off while the holopad is finishing a speech that matters.
/// Only used on steps with freezeWhileSpeaking, and dropped as soon as she goes quiet.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TutorialFrozenComponent : Component
{
    /// <summary>Hard cap. Nothing at the end of a forty minute tutorial is worth a softlock.</summary>
    [ViewVariables]
    public TimeSpan ExpiresAt;
}
