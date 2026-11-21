using Robust.Shared.Audio;

namespace Content.Server._Polonium.Lathe;

/// <summary>
/// Sounds of a lathe at work. The lathe's ambient sound is its loop: it runs from the start of a print run to its end.
/// </summary>
[RegisterComponent, Access(typeof(LatheWorkSoundsSystem))]
public sealed partial class LatheWorkSoundsComponent : Component
{
    /// <summary>
    /// Played when a lathe that stood still starts on an item that takes time.
    /// </summary>
    [DataField]
    public SoundSpecifier? StartSound;

    /// <summary>
    /// Played when the lathe stops: the queue ran out, the print was aborted or the power went.
    /// </summary>
    [DataField]
    public SoundSpecifier? StopSound;

    /// <summary>
    /// Played instead of the other two when the whole run takes no time.
    /// </summary>
    [DataField]
    public SoundSpecifier? InstantSound;

    /// <summary>
    /// The last of the sounds above that was started, cut off when the next one plays.
    /// </summary>
    public EntityUid? Stream;

    /// <summary>
    /// Whether the motor runs: the current run has reached an item that takes time.
    /// </summary>
    public bool MotorRunning;
}
