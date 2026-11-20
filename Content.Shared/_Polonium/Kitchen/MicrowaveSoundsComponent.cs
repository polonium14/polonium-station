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
    /// Played when the door opens as cooking stops, and to the player who opens the window of an idle microwave.
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
    /// Played when something in the chamber catches fire.
    /// </summary>
    [DataField]
    public SoundSpecifier? IgniteSound;

    /// <summary>
    /// How long after the start the fire breaks out. Matches the moment the flames show in the window.
    /// </summary>
    [DataField]
    public TimeSpan IgniteDelay = TimeSpan.FromSeconds(1);

    /// <summary>
    /// When the next spark sound is due.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextSpark;
}
