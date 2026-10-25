using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Modules.Components.Modules;

/// <summary>
/// Speeds up the wearer of the module container while the module is enabled.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SpeedModuleComponent : Component
{
    /// <summary>
    /// Walk speed multiplier applied while the module is enabled.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float WalkModifier = 1f;

    /// <summary>
    /// Sprint speed multiplier applied while the module is enabled.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SprintModifier = 1f;
}
