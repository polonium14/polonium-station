using Content.Shared._Polonium.Kitchen;
using Content.Shared.Kitchen.Components;
using Content.Shared.Kitchen.EntitySystems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Kitchen;

/// <summary>
/// Plays the sounds from <see cref="MicrowaveSoundsComponent"/> to everyone near the microwave.
/// </summary>
public sealed partial class MicrowaveSoundSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MicrowaveSystem _microwave = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ActiveMicrowaveComponent, MicrowaveSoundsComponent>();
        while (query.MoveNext(out var uid, out var active, out var sounds))
        {
            if (!active.Malfunctioning || sounds.NextSpark > now)
                continue;

            sounds.NextSpark = now + _random.Next(sounds.MinSparkInterval, sounds.MaxSparkInterval);
            _audio.PlayPvs(sounds.SparkSound, uid);
        }

        // An instant cook is long over by the time the fire breaks out, so this does not wait on the active microwave.
        var ignitions = EntityQueryEnumerator<MicrowaveIgnitionComponent, MicrowaveSoundsComponent, MicrowaveComponent>();
        while (ignitions.MoveNext(out var uid, out var ignition, out var sounds, out var microwave))
        {
            if (ignition.At > now)
                continue;

            RemCompDeferred<MicrowaveIgnitionComponent>(uid);
            if (!microwave.Broken)
                _audio.PlayPvs(sounds.IgniteSound, uid);
        }
    }

    // The game's own system holds the startup and shutdown of this component,
    // so the start and the end of cooking are caught on the events next to them.
    [SubscribeLocalEvent]
    private void OnCookStart(Entity<ActiveMicrowaveComponent> ent, ref ComponentInit args)
    {
        if (!TryComp(ent, out MicrowaveSoundsComponent? sounds)
            || !TryComp(ent, out MicrowaveComponent? microwave)
            || microwave.Broken)
            return;

        sounds.NextSpark = _timing.CurTime + sounds.MinSparkInterval;
        PlayBurning((ent, microwave), sounds);
        if (HasDoorMoved(ent))
            _audio.PlayPvs(sounds.DoorCloseSound, ent);
    }

    [SubscribeLocalEvent]
    private void OnCookEnd(Entity<ActiveMicrowaveComponent> ent, ref ComponentRemove args)
    {
        if (TerminatingOrDeleted(ent)
            || !HasDoorMoved(ent)
            || !TryComp(ent, out MicrowaveSoundsComponent? sounds)
            || !TryComp(ent, out MicrowaveComponent? microwave)
            || microwave.Broken)
            return;

        _audio.PlayPvs(sounds.DoorOpenSound, ent);
    }

    // An instant cook is over within a tick, the door has no time to move.
    private static bool HasDoorMoved(Entity<ActiveMicrowaveComponent> ent)
    {
        return ent.Comp.TotalTime > 0f;
    }

    private void PlayBurning(Entity<MicrowaveComponent> microwave, MicrowaveSoundsComponent sounds)
    {
        var melted = false;
        var ignited = false;
        foreach (var item in _microwave.GetMicrowaveContents(microwave.AsNullable()))
        {
            // The start swaps a thing that melts for the mess on the spot, and leaves a thing that burns up to be deleted.
            melted |= MetaData(item).CreationTick == _timing.CurTick;
            ignited |= EntityManager.IsQueuedForDeletion(item);
        }

        if (melted)
            _audio.PlayPvs(sounds.BurnSound, microwave);

        if (ignited)
            EnsureComp<MicrowaveIgnitionComponent>(microwave).At = _timing.CurTime + sounds.IgniteDelay;
    }
}
