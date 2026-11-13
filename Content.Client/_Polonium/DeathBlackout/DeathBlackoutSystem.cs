using Content.Shared._Polonium.DeathBlackout;
using Content.Shared.CCVar;
using Content.Shared.DamageOverlay;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Client.Audio;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.DeathBlackout;

/// <summary>
/// Covers the game view and HUD in black and mutes the audio during the death blackout,
/// and fades both gradually while the local player is in critical state. Windows stay above the black.
/// </summary>
public sealed partial class DeathBlackoutSystem : EntitySystem
{
    [Dependency] private IAudioManager _audio = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;

    [Dependency] private EntityQuery<DeathBlackoutComponent> _blackoutQuery = default!;
    [Dependency] private EntityQuery<DamageOverlayComponent> _overlayQuery = default!;
    [Dependency] private EntityQuery<MobStateComponent> _mobStateQuery = default!;
    [Dependency] private EntityQuery<MobThresholdsComponent> _thresholdsQuery = default!;

    // how fast the crit fade catches up with the damage value
    private const float FadeCatchUp = 4f;

    private PanelContainer? _panel;
    private float _level;
    private bool _fadingOut;
    private float _lastGain = -1f;
    private bool _muffled;

    public override void Shutdown()
    {
        base.Shutdown();

        RestoreAudio();

        _panel?.Orphan();
        _panel = null;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var blackout = IsBlackout();
        var target = blackout ? 1f : GetCritFadeTarget();

        if (blackout)
        {
            // hard cut in
            _level = 1f;
            _fadingOut = true;
        }
        else if (_fadingOut)
        {
            // linear fade out at a fixed pace once the blackout ends
            var duration = _cfg.GetCVar(CCVars.DeathBlackoutFadeOut);
            _level = duration > 0f ? MathF.Max(target, _level - frameTime / duration) : target;

            if (_level <= target)
                _fadingOut = false;
        }
        else
        {
            // ease the crit fade, so healing or damage ticks don't make it flicker
            _level += (target - _level) * MathF.Min(1f, frameTime * FadeCatchUp);

            if (target <= 0f && _level < 0.001f)
                _level = 0f;
        }

        UpdatePanel();
        UpdateAudio();
    }

    // true while the player is inside the post death blackout window
    private bool IsBlackout()
    {
        if (!_cfg.GetCVar(CCVars.DeathBlackoutEnabled))
            return false;

        if (_player.LocalEntity is not { } player)
            return false;

        return _blackoutQuery.TryComp(player, out var blackout) && _timing.CurTime < blackout.EndTime;
    }

    // 0 to 1 progress through critical state towards death
    private float GetCritFadeTarget()
    {
        if (!_cfg.GetCVar(CCVars.DeathBlackoutEnabled) || !_cfg.GetCVar(CCVars.DeathBlackoutCritFade))
            return 0f;

        if (_player.LocalEntity is not { } player)
            return 0f;

        if (!_mobStateQuery.TryComp(player, out var mobState) ||
            !_thresholdsQuery.TryComp(player, out var thresholds) ||
            !_overlayQuery.TryComp(player, out var overlay))
            return 0f;

        if (mobState.CurrentState != MobState.Critical)
            return 0f;

        if (!_thresholds.TryGetThresholdForState(player, MobState.Critical, out var start, thresholds) ||
            !_thresholds.TryGetDeadThreshold(player, out var dead, thresholds))
            return 0f;

        var deadThreshold = dead.Value.Float();
        var range = deadThreshold - start.Value.Float();

        if (range <= 0f)
            return 0f;

        // the crit overlay level is the damage as a fraction of the dead threshold and is already networked
        var damage = overlay.CritLevel * deadThreshold;
        var progress = (damage - start.Value.Float()) / range;
        return Math.Clamp(progress, 0f, 1f);
    }

    // the black panel sits right below the window root, so it covers the game view, its popups and the HUD,
    // while windows like the escape menu or ahelp are drawn on top of it
    private void UpdatePanel()
    {
        if (_level <= 0f)
        {
            if (_panel != null)
                _panel.Visible = false;

            return;
        }

        if (_panel == null)
        {
            _panel = new PanelContainer
            {
                Name = "DeathBlackout",
                MouseFilter = Control.MouseFilterMode.Ignore,
                PanelOverride = new StyleBoxFlat { BackgroundColor = Color.Black },
            };
        }

        if (_panel.Parent == null)
        {
            _ui.RootControl.AddChild(_panel);
            _panel.SetPositionInParent(_ui.WindowRoot.GetPositionInParent());
        }

        _panel.Visible = true;
        _panel.Modulate = Color.White.WithAlpha(_level);
    }

    // scales master gain down with the level, squared so the fade is gentle at first
    private void UpdateAudio()
    {
        if (_level <= 0f)
        {
            RestoreAudio();
            return;
        }

        var gain = _cfg.GetCVar(CVars.AudioMasterVolume) * (1f - _level) * (1f - _level);

        _muffled = true;

        if (MathF.Abs(gain - _lastGain) < 0.001f)
            return;

        _lastGain = gain;
        _audio.SetMasterGain(gain);
    }

    // puts master gain back to whatever the volume cvar says
    private void RestoreAudio()
    {
        if (!_muffled)
            return;

        _muffled = false;
        _lastGain = -1f;
        _audio.SetMasterGain(_cfg.GetCVar(CVars.AudioMasterVolume));
    }
}
