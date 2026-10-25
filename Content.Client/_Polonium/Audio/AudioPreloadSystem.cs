using Content.Shared._Polonium.Audio;
using Content.Shared.CCVar;
using Robust.Client.ResourceManagement;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Utility;

namespace Content.Client._Polonium.Audio;

/// <summary>
/// Loads sounds into the resource cache before they are played, see <see cref="PreloadAudioEvent"/>.
/// </summary>
public sealed partial class AudioPreloadSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IResourceCache _resourceCache = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<PreloadAudioEvent>(OnPreload);
    }

    private void OnPreload(PreloadAudioEvent ev)
    {
        if (!WillHear(ev.Kind) || _audio.GetAudioPath(ev.Sound) is not { Length: > 0 } path)
            return;

        _resourceCache.TryGetResource<AudioResource>(new ResPath(path), out _);
    }

    private bool WillHear(PreloadAudioKind kind)
    {
        return kind switch
        {
            PreloadAudioKind.StationEventMusic => _cfg.GetCVar(CCVars.EventMusicEnabled),
            _ => true,
        };
    }
}
