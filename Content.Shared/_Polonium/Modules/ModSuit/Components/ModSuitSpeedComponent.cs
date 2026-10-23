using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Modules.ModSuit.Components;

/// <summary>
/// Applies the speed of enabled speed modules and slows the wearer down when the suit has no power.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ModSuitSpeedComponent : Component
{
    /// <summary>
    /// Walk speed modifier of the suit without power. Replaces the suit's own clothing speed modifier.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float UnpoweredWalkModifier = 1f;

    /// <summary>
    /// Sprint speed modifier of the suit without power. Replaces the suit's own clothing speed modifier.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float UnpoweredSprintModifier = 1f;

    [ViewVariables, AutoNetworkedField]
    public bool Unpowered;
}
