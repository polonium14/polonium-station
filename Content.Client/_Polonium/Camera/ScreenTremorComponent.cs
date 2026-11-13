using System.Numerics;

namespace Content.Client._Polonium.Camera;

/// <summary>
/// Tremors currently shaking the screen of the local player's entity.
/// </summary>
[RegisterComponent, Access(typeof(ScreenTremorSystem))]
public sealed partial class ScreenTremorComponent : Component
{
    [ViewVariables]
    public List<ActiveTremor> Tremors = new();

    [ViewVariables]
    public float Amplitude;

    [ViewVariables]
    public Vector2 Offset;
}

public readonly record struct ActiveTremor(TimeSpan Start, float Duration, float Intensity);
