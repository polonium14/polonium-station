using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Polonium.DeathBlackout;

/// <summary>
/// Added to a mob with a mind that died (or the brain of one gibbed) while in critical state.
/// While it lasts, the client shows a black screen and mutes the audio, and ghosting is blocked.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause, UnsavedComponent]
public sealed partial class DeathBlackoutComponent : Component
{
    /// <summary>
    /// Time the blackout ends at.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan EndTime;
}
