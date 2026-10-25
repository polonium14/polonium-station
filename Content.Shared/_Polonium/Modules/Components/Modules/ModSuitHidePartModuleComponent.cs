using Content.Shared._Polonium.Modules.ModSuit;
using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Modules.Components.Modules;

/// <summary>
/// Hides the suit's own sprite of a part while the module is installed, for modules that draw themselves in its place.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ModSuitHidePartModuleComponent : Component
{
    /// <summary>
    /// The part whose sprite is hidden.
    /// </summary>
    [DataField(required: true)]
    public ModSuitPartType Part;
}
