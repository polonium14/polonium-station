#nullable enable
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._Polonium.Audio;
using Content.Shared._Polonium.Audio;
using Content.Shared.CCVar;
using Robust.Client.ResourceManagement;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Polonium.Audio;

[TestOf(typeof(AudioPreloadSystem))]
public sealed class AudioPreloadTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Fresh = true };

    private static readonly ResPath Music = new("/Audio/_Polonium/Music/eeh.ogg");
    private static readonly ResPath Sound = new("/Audio/_Polonium/Announcements/Shuttle/asteroid_ev_announce.ogg");

    [Test]
    public async Task ClientLoadsWhatItWillHear()
    {
        var cache = Client.ResolveDependency<IResourceCache>();
        await OverrideCVar(Side.Client, CCVars.EventMusicEnabled, false);

        await Preload(Music, PreloadAudioKind.StationEventMusic);
        Assert.That(IsCached(cache, Music), Is.False, "event music was loaded for a player who turned it off");

        await Preload(Sound, PreloadAudioKind.Sound);
        Assert.That(IsCached(cache, Sound), "the sound was not loaded");

        await OverrideCVar(Side.Client, CCVars.EventMusicEnabled, true);

        await Preload(Music, PreloadAudioKind.StationEventMusic);
        Assert.That(IsCached(cache, Music), "the event music was not loaded");
    }

    private async Task Preload(ResPath path, PreloadAudioKind kind)
    {
        var preload = SEntMan.System<AudioPreloadSystem>();
        await Server.WaitPost(() => preload.Preload(new SoundPathSpecifier(path), Filter.SinglePlayer(ServerSession!), kind));
        await Pair.RunTicksSync(5);
    }

    private static bool IsCached(IResourceCache cache, ResPath path)
    {
        return cache.GetAllResources<AudioResource>().Any(res => res.Key == path);
    }
}
