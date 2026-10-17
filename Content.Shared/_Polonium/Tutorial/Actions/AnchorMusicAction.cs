using Robust.Shared.Audio;

namespace Content.Shared._Polonium.Tutorial.Actions;

/// <summary>
/// Looping track played from an anchor. Play resumes a paused track instead of starting it over.
/// </summary>
public sealed partial class AnchorMusicAction : TutorialAction
{
    [DataField(required: true)]
    public string AnchorId = string.Empty;

    [DataField]
    public TutorialMusicMode Mode = TutorialMusicMode.Play;

    /// <summary>Only needed for Play.</summary>
    [DataField]
    public SoundSpecifier? Sound;

    [DataField]
    public float Volume;

    [DataField]
    public float MaxDistance = 12f;
}

public enum TutorialMusicMode : byte
{
    Play,
    Pause,
    Stop,
}
