using Content.Shared.Kitchen.EntitySystems;
using Content.Shared.UserInterface;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._Polonium.Kitchen;

/// <summary>
/// The door of an idle microwave swings open together with its window.
/// Nobody else sees that, so only the player who opened the window hears it.
/// </summary>
public sealed partial class MicrowaveDoorSoundSystem : EntitySystem
{
    [Dependency] private MicrowaveSystem _microwave = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    [SubscribeLocalEvent]
    private void OnWindowOpened(Entity<MicrowaveSoundsComponent> ent, ref AfterActivatableUIOpenEvent args)
    {
        if (!_microwave.IsActiveMicrowave(ent.Owner))
            _audio.PlayLocal(ent.Comp.DoorOpenSound, ent, args.User);
    }
}
