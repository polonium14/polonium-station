using Content.Shared._Polonium.VendingMachines;
using Content.Shared.VendingMachines;
using Content.Shared.VendingMachines.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.VendingMachines;

public sealed partial class VendingMachineKeypadSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = null!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan AudioCooldown = TimeSpan.FromMilliseconds(100);

    // last accepted audio play per sender, so a spammy client can't loop sounds.
    private readonly Dictionary<EntityUid, TimeSpan> _audioCooldowns = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VendingMachineComponent, VendingMachineKeypadAudioMessage>(OnKeypadAudio);
    }

    private void OnKeypadAudio(EntityUid uid, VendingMachineComponent component, VendingMachineKeypadAudioMessage args)
    {
        var now = _timing.CurTime;

        // Sweep expired entries so the table can't grow without bound.
        List<EntityUid>? expired = null;
        foreach (var (actor, last) in _audioCooldowns)
        {
            if (now - last >= AudioCooldown)
                (expired ??= new List<EntityUid>()).Add(actor);
        }
        if (expired is not null)
        {
            foreach (var actor in expired)
                _audioCooldowns.Remove(actor);
        }

        // Rate limit per sender: skip messages that arrive inside the cooldown.
        if (_audioCooldowns.TryGetValue(args.Actor, out var lastPlay) && now - lastPlay < AudioCooldown)
            return;

        _audioCooldowns[args.Actor] = now;

        var (soundPath, volume) = VendingMachineKeypadSounds.Get(args.SoundType);

        if (string.IsNullOrEmpty(soundPath))
            return;

        var audioParams = new AudioParams().WithVolume(volume).WithPitchScale(args.Pitch);

        _audio.PlayPredicted(new SoundPathSpecifier(soundPath), uid, args.Actor, audioParams);
    }
}
