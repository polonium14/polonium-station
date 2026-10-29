using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Polonium.DeathBlackout;

/// <summary>
/// Added to a dead mob (or the brain of a gibbed one) that had a mind.
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
