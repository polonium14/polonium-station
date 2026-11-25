namespace Content.Shared._Polonium.WalkAnimation;

/// <summary>
/// Marks a mob whose arms and legs swing while it walks.
/// The client cuts the whole drawn sprite, clothes included, into fixed limb regions of a 32x32 sprite (arms at the
/// sides, legs in the bottom rows). The fields fit the regions to the species; bodies without separate limbs bob instead.
/// </summary>
[RegisterComponent]
public sealed partial class WalkAnimationComponent : Component
{
    /// <summary>
    /// Distance in tiles covered by one step. Every step swings the other pair of limbs.
    /// </summary>
    [DataField]
    public float StepLength = 1.5f;

    /// <summary>
    /// First sprite row (from the top of the 32x32 sprite) that belongs to the legs.
    /// </summary>
    [DataField]
    public int LegTop = 23;

    /// <summary>
    /// Whether the legs swing. Off for bodies whose legs are not two separate columns (a snail foot, crab legs).
    /// </summary>
    [DataField]
    public bool Legs = true;

    /// <summary>
    /// Whether the arms swing, held items with them.
    /// </summary>
    [DataField]
    public bool Arms = true;

    /// <summary>
    /// Whether the whole sprite rises by a pixel on every step, for bodies without limbs to swing.
    /// </summary>
    [DataField]
    public bool Bob;
}
