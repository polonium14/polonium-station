using Content.Shared._Polonium.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Server._Polonium.Audio;

public sealed partial class AudioPreloadSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;

    /// <summary>
    /// Picks the sound to play and has the clients load it.
    /// </summary>
    /// <returns>The sound to play later.</returns>
    public ResolvedSoundSpecifier Preload(SoundSpecifier sound, Filter filter, PreloadAudioKind kind = PreloadAudioKind.Sound)
    {
        var resolved = _audio.ResolveSound(sound);
        Preload(resolved, filter, kind);
        return resolved;
    }

    public void Preload(ResolvedSoundSpecifier sound, Filter filter, PreloadAudioKind kind = PreloadAudioKind.Sound)
    {
        RaiseNetworkEvent(new PreloadAudioEvent(sound, kind), filter);
    }
}
