using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Camera;

/// <summary>
/// Sent to a player to make their screen tremble like in an earthquake.
/// </summary>
/// <param name="Intensity">How strong the tremor is, 1 being the strongest one.</param>
/// <param name="Duration">How long the tremor lasts, in seconds.</param>
[Serializable, NetSerializable]
public sealed class ScreenTremorEvent(float intensity, float duration) : EntityEventArgs
{
    public readonly float Intensity = intensity;

    public readonly float Duration = duration;
}
