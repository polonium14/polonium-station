using System.Numerics;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Systems.Actions.Widgets;
using Content.Shared.CCVar;
using Robust.Client.Audio;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared;
using Robust.Shared.Audio.Sources;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Abductors;

public sealed partial class AbductorCuratorUIController : UIController, IOnStateExited<GameplayState>
{
    [Dependency] private IAudioManager _audio = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IResourceCache _cache = default!;

    // 110 Hz is exactly 400 samples at this rate, so a blip of whole periods ends on a zero crossing.
    private const int SampleRate = 44000;
    private const int PeriodSamples = 400;
    private const int VoiceSamples = PeriodSamples * 4;
    private const float AttackSamples = 44f;
    private const float ReleaseSamples = 176f;
    private const float SubLevel = 0.35f;
    private const float SecondLevel = 0.16f;
    private const float ThirdLevel = 0.07f;
    private const float Peak = 0.8f;
    private const float VoiceGain = 0.75f;
    private const float ActionColumnWidth = 64f;
    private const float ActionsGap = 16f;

    private readonly Queue<string> _queue = new();
    private AbductorCuratorDialog? _dialog;
    private AudioStream? _voiceStream;
    private IAudioSource? _voice;
    private Vector2? _placedAt;

    public override void Initialize()
    {
        base.Initialize();
        UIManager.OnScreenChanged += OnScreenChanged;
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_dialog is { Visible: true })
            Place(_dialog);
    }

    public void Say(string text)
    {
        _queue.Enqueue(text);
        if (_dialog is not { Visible: true })
            ShowNext();
    }

    public void OnStateExited(GameplayState state)
    {
        _queue.Clear();
        if (_dialog == null)
            return;

        _dialog.Visible = false;
        _dialog.Orphan();
    }

    private void OnScreenChanged((UIScreen? Old, UIScreen? New) screens)
    {
        if (_dialog is { Visible: true })
            Attach(_dialog);
    }

    private void ShowNext()
    {
        if (!_queue.TryDequeue(out var text))
        {
            if (_dialog != null)
                _dialog.Visible = false;
            return;
        }

        _dialog ??= CreateDialog();
        _dialog.Visible = true;
        Attach(_dialog);
        Place(_dialog);
        _dialog.Start(text, _cfg.GetCVar(CCVars.ReducedMotion));
    }

    private AbductorCuratorDialog CreateDialog()
    {
        var dialog = new AbductorCuratorDialog(_cache);
        dialog.LetterShown += Speak;
        dialog.Finished += ShowNext;
        return dialog;
    }

    private void Attach(AbductorCuratorDialog dialog)
    {
        var parent = UIManager.ActiveScreen;
        if (parent == null || dialog.Parent == parent)
            return;

        dialog.Orphan();
        parent.AddChild(dialog);
        _placedAt = null;
    }

    private void Place(AbductorCuratorDialog dialog)
    {
        if (dialog.Parent is not { } parent || UIManager.ActiveScreen?.GetWidget<ActionsBar>() is not { } bar)
            return;

        var actions = bar.ActionsContainer;
        var offset = new Vector2(MathF.Max(actions.Size.X, ActionColumnWidth) + ActionsGap, 0f);
        var position = actions.GlobalPosition - parent.GlobalPosition + offset;
        if (_placedAt is { } placed && Vector2.DistanceSquared(placed, position) < 0.25f)
            return;

        _placedAt = position;
        LayoutContainer.SetPosition(dialog, position);
    }

    private void Speak()
    {
        if (!_cfg.GetCVar(CVars.InterfaceAudio))
            return;

        var gain = VoiceGain * _cfg.GetCVar(CCVars.InterfaceVolume);
        if (gain <= 0f)
            return;

        _voiceStream ??= _audio.LoadAudioRaw(Synthesize(), 1, SampleRate, "abductor-curator-voice");
        if (_voice == null)
        {
            _voice = _audio.CreateAudioSource(_voiceStream);
            if (_voice == null)
                return;

            _voice.Global = true;
        }

        _voice.Gain = gain;
        _voice.Restart();
    }

    private static short[] Synthesize()
    {
        var samples = new short[VoiceSamples];
        for (var i = 0; i < samples.Length; i++)
        {
            var phase = MathF.Tau * i / PeriodSamples;
            var tone = MathF.Sin(phase)
                + SubLevel * MathF.Sin(phase / 2f)
                + SecondLevel * MathF.Sin(phase * 2f)
                + ThirdLevel * MathF.Sin(phase * 3f);
            var envelope = MathF.Min(1f, MathF.Min(i / AttackSamples, (samples.Length - i) / ReleaseSamples));
            samples[i] = (short) (tone * envelope * Peak * short.MaxValue);
        }

        return samples;
    }
}
