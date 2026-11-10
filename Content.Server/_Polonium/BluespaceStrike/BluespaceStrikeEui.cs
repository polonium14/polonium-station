// SPDX-FileCopyrightText: 2026 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration.Logs;
using Content.Server.AlertLevel;
using Content.Server.Audio;
using Content.Server.EUI;
using Content.Server.Station.Systems;
using Content.Shared._Polonium.BluespaceStrike;
using Content.Shared.AlertLevel;
using Content.Shared.Database;
using Content.Shared.Eui;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.BluespaceStrike;

public sealed partial class BluespaceStrikeEui : BaseEui
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    private static readonly ResolvedSoundSpecifier ArtilleryAnnounceSound =
        new ResolvedPathSpecifier("/Audio/_Polonium/Admeme/artyleria.ogg");
    private const float ArtilleryAnnounceVolume = 17f;
    private static readonly TimeSpan ArtilleryAnnounceCooldown = TimeSpan.FromSeconds(20);

    private readonly BluespaceStrikeSystem _strike;
    private readonly AlertLevelSystem _alertLevel;
    private readonly ServerStationSystem _station;
    private readonly ServerGlobalSoundSystem _globalSound;
    private readonly ISawmill _sawmill;
    private TimeSpan _nextArtillerySound = TimeSpan.Zero;

    public BluespaceStrikeEui(IDependencyCollection deps)
    {
        deps.InjectDependencies(this, oneOff: true);

        _strike = _entities.System<BluespaceStrikeSystem>();
        _alertLevel = _entities.System<AlertLevelSystem>();
        _station = _entities.System<ServerStationSystem>();
        _globalSound = _entities.System<ServerGlobalSoundSystem>();
        _sawmill = _logManager.GetSawmill("bluespace-strike");
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

        if (!_alertLevel.TryGetLevel(station.Value, out var levelProto))
            return (false, string.Empty);

        var level = levelProto.Value.Id;
        var isEpsilon = string.Equals(level, "epsilon", StringComparison.OrdinalIgnoreCase);
        return (isEpsilon, level);
    }

    private void HandlePreview(BluespaceStrikeEuiMsg.PreviewRequest request)
    {
        if (_strike.GeneratePreview(request.Epicenter, request.Radius) is not { } preview)
        {
            _sawmill.Warning($"Failed to generate bluespace strike preview at {request.Epicenter} r={request.Radius}");
            return;
        }

        SendMessage(preview);
    }

    private void HandleConfirm(BluespaceStrikeEuiMsg.Confirm confirm)
    {
        var strike = _strike.ScheduleStrike(
            confirm.Epicenter,
            confirm.Radius,
            confirm.DelaySeconds,
            confirm.ShowMarkersAndSound,
            Player.AttachedEntity);

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

        var audio = AudioParams.Default.WithVolume(ArtilleryAnnounceVolume);
        var filter = Filter.Empty().AddAllPlayers(_playerManager);
        _globalSound.PlayAdminGlobal(filter, ArtilleryAnnounceSound, audio);
        _nextArtillerySound = _timing.CurTime + ArtilleryAnnounceCooldown;

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{Player} played bluespace artillery announce sound via artbs EUI");
    }
}
