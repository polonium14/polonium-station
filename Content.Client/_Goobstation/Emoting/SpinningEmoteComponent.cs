using Robust.Client.GameObjects;

namespace Content.Client.Emoting;

/// <summary>
///     Tracks an in-progress spin emote, which cycles the sprite's facing instead of rotating it.
///     Client-only: <see cref="SpriteComponent.DirectionOverride"/> is never networked.
/// </summary>
[RegisterComponent]
public sealed partial class SpinningEmoteComponent : Component
{
    /// <summary>
    ///     Seconds spent on the current facing.
    /// </summary>
    public float Accumulator;

    /// <summary>
    ///     How many facings have been shown so far.
    /// </summary>
    public int Step;

    /// <summary>
    ///     Sprite override state from before the spin, restored once it ends.
    /// </summary>
    public Direction PreviousDirection;

    public bool PreviousEnabled;
}
