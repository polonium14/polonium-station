using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Audio;

/// <summary>
/// Tells the client to load a sound before it is played. The client decodes a whole file at once the first time it
/// plays it, which freezes the game for a moment on long tracks, so this lets it happen at a less noticeable time.
/// </summary>
[Serializable, NetSerializable]
public sealed class PreloadAudioEvent(ResolvedSoundSpecifier sound, PreloadAudioKind kind) : EntityEventArgs
{
    public ResolvedSoundSpecifier Sound = sound;

    public PreloadAudioKind Kind = kind;
}

/// <summary>
/// What the preloaded sound is going to be played as, so clients that won't hear it can skip it.
/// </summary>
public enum PreloadAudioKind : byte
{
    Sound,

    /// <summary>
    /// Played as station event music, see <see cref="CCVar.CCVars.EventMusicEnabled"/>.
    /// </summary>
    StationEventMusic,
}
