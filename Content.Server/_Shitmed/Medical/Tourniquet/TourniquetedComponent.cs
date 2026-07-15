namespace Content.Server._Shitmed.Medical.Tourniquet;

/// <summary>
/// Marker on an organ currently under a tourniquet - lets TourniquetSystem apply the
/// "TourniquetPresent" bleed-block modifier to any NEW wound created on this organ while the
/// tourniquet is on, not just the wounds that already existed at application time (see
/// TourniquetSystem's own OnWoundAddedToTourniquetedOrgan).
/// </summary>
[RegisterComponent]
public sealed partial class TourniquetedComponent : Component
{
    /// <summary>
    /// The tourniquet item entity responsible for this organ's effects - lets organ-removal
    /// cleanup find and strip the right modifiers/container entry without scanning every
    /// TourniquetComponent in existence.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid TourniquetEntity;
}
