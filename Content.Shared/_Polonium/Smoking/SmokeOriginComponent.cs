using System.Numerics;

namespace Content.Shared._Polonium.Smoking;

/// <summary>
/// Where the smoke of a lit smokable comes from when it lies on the floor. Smokables without it use
/// <see cref="CigaretteEmber"/>.
/// </summary>
[RegisterComponent]
public sealed partial class SmokeOriginComponent : Component
{
    /// <summary>
    /// The ember of the standard cigarette sprite.
    /// </summary>
    public static readonly Vector2 CigaretteEmber = new(-0.125f, -0.156f);

    /// <summary>
    /// Offset of the ember (or the bowl of a pipe) from the sprite centre, in tiles, X right and Y up, before the
    /// item's rotation. Read off the lit-icon sprite: (pixel - 16) / 32, with Y flipped.
    /// </summary>
    [DataField]
    public Vector2 Offset = CigaretteEmber;
}
