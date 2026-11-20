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
        if (BurnedContents((ent, microwave)))
            _audio.PlayPvs(sounds.BurnSound, ent);

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

    private bool BurnedContents(Entity<MicrowaveComponent> microwave)
    {
        foreach (var item in _microwave.GetMicrowaveContents(microwave.AsNullable()))
        {
            // The start either swaps a thing for the mess on the spot or leaves it to be deleted.
            if (EntityManager.IsQueuedForDeletion(item) || MetaData(item).CreationTick == _timing.CurTick)
                return true;
        }

        return false;
    }
}
