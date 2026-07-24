// SPDX-FileCopyrightText: 2026 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration.Logs;
using Content.Server.AlertLevel;
using Content.Server.Audio;
using Content.Server.EUI;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared._Polonium.BluespaceStrike;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.BluespaceStrike;

[UsedImplicitly]
public sealed class BluespaceStrikeEui : BaseEui
{
    private readonly BluespaceStrikeSystem _strike;
    private readonly ExplosionSystem _explosion;
    private readonly AlertLevelSystem _alertLevel;
    private readonly StationSystem _station;
    private readonly ServerGlobalSoundSystem _globalSound;
    private readonly IAdminLogManager _adminLog;
    private readonly IPlayerManager _playerManager;
    private readonly IGameTiming _timing;
    private readonly ISawmill _sawmill;
    private TimeSpan _nextArtillerySound = TimeSpan.Zero;

    public BluespaceStrikeEui()
    {
        var sys = IoCManager.Resolve<IEntitySystemManager>();
        _strike = sys.GetEntitySystem<BluespaceStrikeSystem>();
        _explosion = sys.GetEntitySystem<ExplosionSystem>();
        _alertLevel = sys.GetEntitySystem<AlertLevelSystem>();
        _station = sys.GetEntitySystem<StationSystem>();
        _globalSound = sys.GetEntitySystem<ServerGlobalSoundSystem>();
        _adminLog = IoCManager.Resolve<IAdminLogManager>();
        _playerManager = IoCManager.Resolve<IPlayerManager>();
        _timing = IoCManager.Resolve<IGameTiming>();
        _sawmill = IoCManager.Resolve<ILogManager>().GetSawmill("bluespace-strike");
    }

    public override void Opened()
    {
        base.Opened();
        _strike.RegisterEui(this);
        StateDirty();
    }

    public override void Closed()
    {
        base.Closed();
        _strike.UnregisterEui(this);
    }

    public override EuiStateBase GetNewState()
    {
        var (isEpsilon, level) = GetStationAlertInfo();
        return new BluespaceStrikeEuiState
        {
            IsEpsilon = isEpsilon,
            CurrentAlertLevel = level,
        };
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        switch (msg)
        {
            case BluespaceStrikeEuiMsg.PreviewRequest request:
                HandlePreview(request);
                break;
            case BluespaceStrikeEuiMsg.Confirm confirm:
                HandleConfirm(confirm);
                break;
            case BluespaceStrikeEuiMsg.PlayArtillerySound:
                HandlePlayArtillerySound();
                break;
        }
    }

    private (bool IsEpsilon, string Level) GetStationAlertInfo()
    {
        EntityUid? station = null;

        if (Player.AttachedEntity is { } playerEnt)
            station = _station.GetOwningStation(playerEnt);

        if (station == null)
        {
            var stations = _station.GetStations();
            foreach (var candidate in stations)
            {
                station = candidate;
                break;
            }
        }

        if (station == null)
            return (false, string.Empty);

        var level = _alertLevel.GetLevel(station.Value);
        var isEpsilon = string.Equals(level, "epsilon", StringComparison.OrdinalIgnoreCase);
        return (isEpsilon, level);
    }

    private void HandlePreview(BluespaceStrikeEuiMsg.PreviewRequest request)
    {
        if (request.Radius <= 0)
            return;

        var slope = BluespaceStrikeComponent.DefaultSlope;
        var maxIntensity = BluespaceStrikeComponent.DefaultMaxIntensity;
        var totalIntensity = _explosion.RadiusToIntensity(request.Radius, slope, maxIntensity);

        if (totalIntensity <= 0)
            return;

        var previewRequest = new SpawnExplosionEuiMsg.PreviewRequest(
            request.Epicenter,
            BluespaceStrikeComponent.ExplosionType,
            totalIntensity,
            slope,
            maxIntensity);

        var explosion = _explosion.GenerateExplosionPreview(previewRequest);
        if (explosion == null)
        {
            _sawmill.Error("Failed to generate bluespace strike preview.");
            return;
        }

        SendMessage(new BluespaceStrikeEuiMsg.PreviewData(explosion, slope, totalIntensity));
    }

    private void HandleConfirm(BluespaceStrikeEuiMsg.Confirm confirm)
    {
        var user = Player.AttachedEntity;
        var strike = _strike.ScheduleStrike(
            confirm.Epicenter,
            confirm.Radius,
            confirm.DelaySeconds,
            confirm.ShowMarkersAndSound,
            user);

        if (strike == null)
        {
            _sawmill.Warning($"Failed to schedule bluespace strike at {confirm.Epicenter}");
            return;
        }

        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{Player} confirmed bluespace strike EUI at {confirm.Epicenter} r={confirm.Radius} t={confirm.DelaySeconds}s warn={confirm.ShowMarkersAndSound}");
    }

    private void HandlePlayArtillerySound()
    {
        if (_timing.CurTime < _nextArtillerySound)
            return;

        // playglobalsound <path> <volume>
        var audio = AudioParams.Default
            .WithVolume(BluespaceStrikeComponent.ArtilleryAnnounceVolume)
            .AddVolume(-8);
        var filter = Filter.Empty().AddAllPlayers(_playerManager);
        _globalSound.PlayAdminGlobal(filter, BluespaceStrikeComponent.ArtilleryAnnounceSound, audio);
        _nextArtillerySound = _timing.CurTime + BluespaceStrikeComponent.ArtilleryAnnounceCooldown;

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{Player} played bluespace artillery announce sound via artbs EUI");
    }
}
