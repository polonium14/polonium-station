using Content.Server.Lathe.Components;
using Content.Shared.Audio;
using Content.Shared.Lathe;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Polonium.Lathe;

/// <summary>
/// Plays the sounds from <see cref="LatheWorkSoundsComponent"/> and switches the lathe's ambient loop with its work.
/// </summary>
public sealed partial class LatheWorkSoundsSystem : EntitySystem
{
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LatheWorkSoundsComponent, LatheStartPrintingEvent>(OnStartPrinting);
        // The lathe's own system holds the shutdown of this component, so the end of a run is caught on its removal.
        SubscribeLocalEvent<LatheProducingComponent, ComponentRemove>(OnRunEnd);
    }

    private void OnStartPrinting(Entity<LatheWorkSoundsComponent> ent, ref LatheStartPrintingEvent args)
    {
        // The motor spins up on the first item of a run that takes any time.
        if (ent.Comp.MotorRunning
            || !TryComp(ent, out LatheProducingComponent? producing)
            || producing.ProductionLength <= TimeSpan.Zero)
            return;

        ent.Comp.MotorRunning = true;
        _audio.Stop(ent.Comp.Stream);
        ent.Comp.Stream = _audio.PlayPvs(ent.Comp.StartSound, ent)?.Entity;
        _ambient.SetAmbience(ent, true);
    }

    private void OnRunEnd(Entity<LatheProducingComponent> ent, ref ComponentRemove args)
    {
        if (TerminatingOrDeleted(ent) || !TryComp(ent, out LatheWorkSoundsComponent? sounds))
            return;

        _audio.Stop(sounds.Stream);
        if (!sounds.MotorRunning)
        {
            sounds.Stream = _audio.PlayPvs(sounds.InstantSound, ent)?.Entity;
            return;
        }

        sounds.MotorRunning = false;
        _ambient.SetAmbience(ent, false);
        sounds.Stream = _audio.PlayPvs(sounds.StopSound, ent)?.Entity;
    }
}
