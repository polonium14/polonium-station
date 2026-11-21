using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Polonium.Kitchen;

/// <summary>
/// Marks a microwave in which something has started to burn and is about to be heard catching fire.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(MicrowaveSoundSystem))]
public sealed partial class MicrowaveIgnitionComponent : Component
{
    /// <summary>
    /// When the fire breaks out.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan At;
}
