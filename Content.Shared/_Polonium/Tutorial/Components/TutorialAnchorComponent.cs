using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Tutorial.Components;

/// <summary>
/// Stick this on a door/item/whatever in the map editor to make it targetable by tutorial steps.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TutorialAnchorComponent : Component
{
    /// <summary>Has to match what's in the flow YAML (e.g. "first_door").</summary>
    [DataField(required: true), AutoNetworkedField]
    public string AnchorId = string.Empty;

    /// <summary>A lesson prop the trainee has to take apart, so the map furniture lock skips it.</summary>
    [DataField]
    public bool AllowDeconstruct;
}
