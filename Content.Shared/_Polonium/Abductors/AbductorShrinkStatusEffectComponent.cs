using System.Numerics;

namespace Content.Shared._Polonium.Abductors;

/// <summary>
/// Status effect of the shrink ray: scales the target's sprite for as long as it lasts.
/// </summary>
[RegisterComponent, Access(typeof(AbductorShrinkSystem))]
public sealed partial class AbductorShrinkStatusEffectComponent : Component
{
    [DataField]
    public float Scale = 0.5f;

    [ViewVariables]
    public Vector2 OriginalScale = Vector2.One;

    [ViewVariables]
    public bool HadScaleVisuals;
}
