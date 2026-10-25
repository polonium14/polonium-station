using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Modules.Components.Modules;

/// <summary>
/// Shows the module on the suit. Every sprite state of the suit's parts that has a "-suffix" variant gets it drawn on top.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ModSuitModuleOverlayComponent : Component
{
    /// <summary>
    /// Suffix of the overlay states shown while the module is installed.
    /// </summary>
    [DataField(required: true)]
    public string Suffix = string.Empty;

    /// <summary>
    /// Suffix used instead of <see cref="Suffix"/> while the module is enabled.
    /// </summary>
    [DataField]
    public string? EnabledSuffix;
}
