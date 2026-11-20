using Robust.Shared.Audio;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Polonium.Kitchen;

/// <summary>
/// Sounds of a working microwave that go with what its window shows: the door, sparks from metal and burning contents.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class MicrowaveSoundsComponent : Component
{
    /// <summary>
    /// Played when the door shuts as cooking starts.
    /// </summary>
    [DataField]
    public SoundSpecifier? DoorCloseSound;

    /// <summary>
    /// Played when the door opens as cooking stops, and to a player whose window opens on an idle microwave.
    /// </summary>
    [DataField]
    public SoundSpecifier? DoorOpenSound;

    /// <summary>
    /// Played now and then while the contents make the microwave malfunction.
    /// </summary>
    [DataField]
    public SoundSpecifier? SparkSound;

    /// <summary>
    /// Shortest pause between two spark sounds.
    /// </summary>
    [DataField]
    public TimeSpan MinSparkInterval = TimeSpan.FromSeconds(0.8);

    /// <summary>
    /// Longest pause between two spark sounds.
    /// </summary>
    [DataField]
    public TimeSpan MaxSparkInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Played when the start of cooking burns something in the chamber.
    /// </summary>
    [DataField]
    public SoundSpecifier? BurnSound;

    /// <summary>
    /// When the next spark sound is due.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextSpark;
}
