using Content.Shared.Whitelist;

namespace Content.Shared._Polonium.Kitchen;

/// <summary>
/// Tells the microwave window which contents react visibly when the microwave runs.
/// </summary>
[RegisterComponent]
public sealed partial class MicrowaveReactionVisualsComponent : Component
{
    /// <summary>
    /// Items that throw sparks for a moment after the microwave starts.
    /// </summary>
    [DataField]
    public EntityWhitelist? Sparking;
}
