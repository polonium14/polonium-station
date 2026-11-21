using Content.Shared.CCVar;
using Robust.Client.Audio;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._Polonium.UserInterface.PixelUi;

/// <summary>
/// Makes the keys of a device window sound like real switches: one click as a key goes down, another as it comes back up.
/// </summary>
public sealed partial class PixelKeySoundUIController : UIController
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [UISystemDependency] private readonly AudioSystem? _audio = default;

    // The same gain the stock interface click is played with, the files are levelled against that click.
    private const float Gain = 0.25f;
    private const float PitchVariation = 0.03f;

    private static readonly ProtoId<SoundCollectionPrototype> DownSounds = "PixelKeyDown";
    private static readonly ProtoId<SoundCollectionPrototype> UpSounds = "PixelKeyUp";

    private readonly SoundSpecifier _down = new SoundCollectionSpecifier(DownSounds);
    private readonly SoundSpecifier _up = new SoundCollectionSpecifier(UpSounds);

    private BaseButton? _held;
    private EntityUid? _signal;

    /// <summary>
    /// Replaces the stock click and hover sounds of a button with the key sounds.
    /// </summary>
    /// <param name="down">The press sound of a key that is built differently from the rest.</param>
    public void Attach(BaseButton key, SoundSpecifier? down = null)
    {
        key.MuteSounds = true;
        key.OnButtonDown += _ =>
        {
            _held = key;
            Play(down ?? _down);
        };
        key.OnButtonUp += _ =>
        {
            if (_held != key)
                return;

            _held = null;
            Play(_up);
        };
    }

    /// <summary>
    /// Plays a sound of the device itself, such as a buzzer. It cuts the previous one off, so fast clicks do not pile up.
    /// </summary>
    public void PlaySignal(SoundSpecifier sound)
    {
        _audio?.Stop(_signal);
        _signal = Play(sound);
    }

    private EntityUid? Play(SoundSpecifier sound)
    {
        var gain = Gain * _cfg.GetCVar(CCVars.InterfaceVolume);
        if (_audio == null || gain <= 0f || !_cfg.GetCVar(CVars.InterfaceAudio))
            return null;

        var audioParams = AudioParams.Default
            .WithVolume(SharedAudioSystem.GainToVolume(gain))
            .WithVariation(PitchVariation);
        return _audio.PlayGlobal(sound, Filter.Local(), false, audioParams)?.Entity;
    }
}
