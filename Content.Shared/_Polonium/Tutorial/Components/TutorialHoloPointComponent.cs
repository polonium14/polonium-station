using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Tutorial.Components;

/// <summary>
/// Floor projector the tutorial assistant can appear on. Which pad is which is TutorialAnchor.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TutorialHoloPointComponent : Component
{
    /// <summary>Mentor hologram parked on this pad, if any.</summary>
    [ViewVariables]
    public EntityUid? Projection;
}

[Serializable, NetSerializable]
public enum TutorialHoloPointVisuals : byte
{
    Active,
}
