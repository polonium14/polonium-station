using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client._Polonium.WalkAnimation;

/// <summary>
/// Client-side step state of a <see cref="Content.Shared._Polonium.WalkAnimation.WalkAnimationComponent"/> entity.
/// </summary>
[RegisterComponent, Access(typeof(WalkAnimationSystem))]
public sealed partial class WalkAnimationStateComponent : Component
{
    /// <summary>
    /// Steps walked so far. The integer part picks which limbs swing, the fraction is how far into the step they are.
    /// </summary>
    [ViewVariables]
    public float Steps;

    [ViewVariables]
    public EntityUid LastParent;

    [ViewVariables]
    public Vector2 LastPosition;

    [ViewVariables]
    public TimeSpan LastMove;

    /// <summary>
    /// Whether the post-shader is on the sprite. It only stays there while walking, because it costs an extra render pass.
    /// </summary>
    [ViewVariables]
    public bool Active;

    public ShaderInstance? Shader;

    /// <summary>
    /// Layers drawn by client-side child entities while walking, outside the post-shader: the tail and wings, so the limb
    /// regions do not cut pieces out of them, and the held items, so they move with the hand holding them.
    /// </summary>
    [ViewVariables]
    public List<WalkDetachedGroup> Detached = new();
}

public enum WalkDetachedPart : byte
{
    Tail,
    RightHand,
    LeftHand,
}

/// <summary>
/// A child entity that draws one <see cref="WalkDetachedPart"/> of the mob while it walks.
/// </summary>
public sealed class WalkDetachedGroup(WalkDetachedPart part, EntityUid copy)
{
    public readonly WalkDetachedPart Part = part;
    public readonly EntityUid Copy = copy;
    public readonly List<WalkDetachedLayer> Layers = new();

    /// <summary>
    /// Transparent layer that keeps the bottom of the copy's bounds just below the mob's, so the renderer's y-sort always
    /// draws the copy right after the mob.
    /// </summary>
    public SpriteComponent.Layer? Anchor;
}

/// <summary>
/// A mob layer, hidden while walking, and its copy on the <see cref="WalkDetachedGroup"/> entity.
/// </summary>
public sealed class WalkDetachedLayer(SpriteComponent.Layer mob, SpriteComponent.Layer copy, Color color)
{
    public readonly SpriteComponent.Layer Mob = mob;
    public readonly SpriteComponent.Layer Copy = copy;

    /// <summary>
    /// The colour the mob layer should have. It is drawn fully transparent while the copy shows it.
    /// </summary>
    public Color Color = color;
}
