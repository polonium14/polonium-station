using Content.Shared._Polonium.Tutorial.Actions;
using Content.Shared._Polonium.Tutorial.Components;
using Content.Shared.Buckle;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Polonium.Tutorial;

public sealed partial class TutorialActionExecutor
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBuckleSystem _buckle = default!;
    [Dependency] private SharedStunSystem _stun = default!;

    private void SetFlag(EntityUid player, SetFlagAction action)
    {
        if (TryComp<TutorialSessionComponent>(player, out var session))
            session.Flags.Add(action.Flag);
    }

    private void AnchorMusic(EntityUid player, AnchorMusicAction action, bool instant)
    {
        if (!TryGetAnchor(player, action.AnchorId, out var source))
            return;

        var music = EnsureComp<TutorialMusicComponent>(source);
        var stream = music.Stream is { } existing && !TerminatingOrDeleted(existing) ? existing : (EntityUid?) null;

        switch (action.Mode)
        {
            case TutorialMusicMode.Play:
                // a debug skip replays room setup, nobody needs the track from a room they never saw
                if (instant)
                    return;

                if (stream is { } paused && TryComp<AudioComponent>(paused, out var audio))
                {
                    _audio.SetState(paused, AudioState.Playing, component: audio);
                    return;
                }

                if (action.Sound is not { } sound)
                    return;

                var audioParams = AudioParams.Default
                    .WithLoop(true)
                    .WithVolume(action.Volume)
                    .WithMaxDistance(action.MaxDistance);

                music.Stream = _audio.PlayPvs(sound, source, audioParams)?.Entity;
                break;

            case TutorialMusicMode.Pause:
                if (stream is { } playing)
                    _audio.SetState(playing, AudioState.Paused);
                break;

            case TutorialMusicMode.Stop:
                _audio.Stop(stream);
                music.Stream = null;
                break;
        }
    }

    private void Eject(EntityUid player, EjectTraineeAction action)
    {
        if (!TryComp<TutorialSessionComponent>(player, out var session))
            return;

        _buckle.Unbuckle((player, null), null);
        Teleport(player, action.AnchorId);
        _audio.PlayPvs(action.Sound, player);
        _stun.TryKnockdown((player, null), TimeSpan.FromSeconds(action.KnockdownSeconds), drop: false, force: true);

        // the tracker is still walking this step's watchers, it switches once that pass ends
        session.JumpTo = action.Step;
    }
}
