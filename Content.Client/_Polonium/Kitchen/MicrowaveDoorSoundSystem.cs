using Content.Shared._Polonium.Kitchen;
using Content.Shared.Kitchen.Components;
using Content.Shared.Kitchen.EntitySystems;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Kitchen;

/// <summary>
/// Plays the door of a microwave and the beep of a finished cook the moment this client sees them happen.
/// The client predicts the cooking, so the same sounds sent by the server came a few ticks after the door had moved.
/// </summary>
public sealed partial class MicrowaveDoorSoundSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    // Component events cannot tell a real change from prediction rolling back and replaying,
    // so this compares what each frame ends on.
    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var player = _player.LocalEntity;
        var query = EntityQueryEnumerator<MicrowaveSoundsComponent, MicrowaveComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var sounds, out var microwave, out var meta))
        {
            if ((meta.Flags & MetaDataFlags.Detached) != 0)
            {
                sounds.Seen = null;
                continue;
            }

            // An instant cook is over within a tick, the door has no time to move.
            var cooking = TryComp(uid, out ActiveMicrowaveComponent? active) && active.TotalTime > 0f;
            var watched = player != null && _ui.IsUiOpen(uid, MicrowaveUiKey.Key, player.Value);
            var before = sounds.Seen;
            sounds.Seen = new MicrowaveDoorView(cooking, watched, active?.CookTimeEnd ?? TimeSpan.Zero);

            if (before is not { } seen || microwave.Broken)
                continue;

            if (seen.Cooking != cooking)
            {
                Play(cooking ? sounds.DoorCloseSound : sounds.DoorOpenSound, uid);
                if (!cooking && seen.CookEnd < _timing.CurTime)
                    Play(microwave.FoodDoneSound, uid);
            }
            else if (!cooking && seen.Watched != watched)
            {
                Play(watched ? sounds.DoorOpenSound : sounds.DoorCloseSound, uid);
            }
        }
    }

    private void Play(SoundSpecifier? sound, EntityUid microwave)
    {
        _audio.PlayEntity(sound, Filter.Local(), microwave, false);
    }
}
