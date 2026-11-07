using Content.Shared._Polonium.Hearing;
using Robust.Client.Audio;
using Robust.Client.Player;
using Robust.Shared.Audio.Components;

namespace Content.Client._Polonium.Hearing;

/// <summary>
/// Turns the world down for a local player with switched-on headphones. Only positional sounds are touched: global
/// ones are interface clicks, lobby and admin music, which the headphones let through. The engine sets each sound's
/// volume back to its own every audio tick, so this sets an absolute value right after it and needs no cleanup.
/// </summary>
public sealed partial class HearingMufflingAudioSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private HearingMufflingSystem _muffling = default!;

    public override void Initialize()
    {
        base.Initialize();

        UpdatesAfter.Add(typeof(AudioSystem));
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_player.LocalEntity is not { } local || !_muffling.TryGetMuffling(local, out var muffling))
            return;

        var query = AllEntityQuery<AudioComponent, TransformComponent>();
        while (query.MoveNext(out var comp, out var xform))
        {
            // the headphones' own on/off click stays audible
            if (comp.Global || xform.ParentUid == muffling.Owner)
                continue;

            comp.Volume = comp.Params.Volume + muffling.Comp.VolumeModifier;
        }
    }
}
