using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Server.EUI;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared._Polonium.Administration;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Eui;
using Content.Shared.Players.PlayTimeTracking;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server._Polonium.Administration;

public sealed partial class TimeTransferPanelEui : BaseEui
{
    private static readonly Regex PlainMinutes = new(@"^\d{1,9}$", RegexOptions.Compiled);
    private static readonly Regex Duration = new(@"^(?:\d{1,6}(?:y|mo|w|d|h|m))+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DurationPart = new(@"(\d+)(y|mo|w|d|h|m)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private const long MaxMinutes = 100L * 525960;

    [Dependency] private IAdminManager _adminMan = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private ILogManager _log = default!;
    [Dependency] private IPlayerLocator _playerLocator = default!;
    [Dependency] private IPlayerManager _playerMan = default!;
    [Dependency] private IPrototypeManager _protoMan = default!;
    [Dependency] private IServerDbManager _databaseMan = default!;
    [Dependency] private PlayTimeTrackingManager _playTimeMan = default!;

    private readonly ISawmill _sawmill;

    public TimeTransferPanelEui()
    {
        IoCManager.InjectDependencies(this);

        _sawmill = _log.GetSawmill("admin.time_eui");
    }

    public override EuiStateBase GetNewState()
    {
        return new TimeTransferPanelEuiState(_adminMan.HasAdminFlag(Player, AdminFlags.Moderator));
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is not TimeTransferEuiMessage message)
            return;

        TransferTime(message.PlayerId, message.TimeData, message.Overwrite);
    }

    private async void TransferTime(string playerId, List<TimeTransferData> timeData, bool overwrite)
    {
        try
        {
            await TransferTimeAsync(playerId, timeData, overwrite);
        }
        catch (Exception e)
        {
            _sawmill.Error($"{Player.Name} ({Player.UserId}) failed to transfer play time: {e}");
            SendWarning(Loc.GetString("time-transfer-panel-warning-error"), Color.Red);
        }
    }

    private async Task TransferTimeAsync(string playerId, List<TimeTransferData> timeData, bool overwrite)
    {
        if (!_adminMan.HasAdminFlag(Player, AdminFlags.Moderator))
        {
            _sawmill.Warning($"{Player.Name} ({Player.UserId}) tried to change play time without the moderator flag");
            return;
        }

        if (timeData.Count == 0)
        {
            SendWarning(Loc.GetString("time-transfer-panel-warning-no-job"), Color.Red);
            return;
        }

        var times = new Dictionary<string, TimeSpan>();
        foreach (var data in timeData)
        {
            if (!_protoMan.HasIndex<PlayTimeTrackerPrototype>(data.PlaytimeTracker))
            {
                _sawmill.Warning($"{Player.Name} ({Player.UserId}) sent unknown play time tracker {data.PlaytimeTracker}");
                SendWarning(Loc.GetString("time-transfer-panel-warning-bad-tracker", ("tracker", data.PlaytimeTracker)), Color.Red);
                return;
            }

            if (ParseTime(data.TimeString) is not { } time)
            {
                SendWarning(Loc.GetString("time-transfer-panel-warning-bad-time", ("tracker", data.PlaytimeTracker), ("time", data.TimeString)), Color.Red);
                return;
            }

            times[data.PlaytimeTracker] = time;
        }

        var located = await _playerLocator.LookupIdByNameOrIdAsync(playerId);
        if (located == null)
        {
            _sawmill.Warning($"{Player.Name} ({Player.UserId}) tried to change play time of a non-existing player {playerId}");
            SendWarning(Loc.GetString("time-transfer-panel-no-player-database-message"), Color.Red);
            return;
        }

        if (_playerMan.TryGetSessionById(located.UserId, out var session) && _playTimeMan.TryGetTrackerTimes(session, out _))
            ApplyToOnlinePlayer(session, times, overwrite);
        else
            await ApplyToDatabase(located.UserId, times, overwrite);

        _adminLog.Add(LogType.AdminCommands,
            LogImpact.High,
            $"{Player:actor} {(overwrite ? "set" : "added")} play time of {located.Username} for trackers: {string.Join(", ", times.Select(t => $"{t.Key} = {t.Value}"))}");
        _sawmill.Info($"{Player.Name} ({Player.UserId}) {(overwrite ? "set" : "added")} {times.Count} play time trackers for {located.Username} ({located.UserId})");

        SendWarning(Loc.GetString(overwrite ? "time-transfer-panel-warning-set-success" : "time-transfer-panel-warning-add-success"), Color.LightGreen);
    }

    /// <summary>
    /// The online player's tracker times live in memory and are flushed to the database over them,
    /// so they have to be changed there and not directly in the database.
    /// </summary>
    private void ApplyToOnlinePlayer(ICommonSession session, Dictionary<string, TimeSpan> times, bool overwrite)
    {
        foreach (var (tracker, time) in times)
        {
            var delta = overwrite ? time - _playTimeMan.GetPlayTimeForTracker(session, tracker) : time;
            _playTimeMan.AddTimeToTracker(session, tracker, delta);
        }

        _playTimeMan.QueueSendTimers(session);
        _playTimeMan.SaveSession(session);
    }

    private async Task ApplyToDatabase(NetUserId userId, Dictionary<string, TimeSpan> times, bool overwrite)
    {
        var stored = new Dictionary<string, TimeSpan>();
        if (!overwrite)
        {
            foreach (var playTime in await _databaseMan.GetPlayTimes(userId))
            {
                stored[playTime.Tracker] = playTime.TimeSpent;
            }
        }

        var updates = new List<PlayTimeUpdate>(times.Count);
        foreach (var (tracker, time) in times)
        {
            updates.Add(new PlayTimeUpdate(userId, tracker, time + stored.GetValueOrDefault(tracker)));
        }

        await _databaseMan.UpdatePlayTimes(updates);
    }

    /// <summary>
    /// Parses plain minutes or a sum of parts like <c>1y2mo3w4d5h6m</c>.
    /// </summary>
    private static TimeSpan? ParseTime(string timeString)
    {
        if (PlainMinutes.IsMatch(timeString))
        {
            var plain = long.Parse(timeString);
            return plain > MaxMinutes ? null : TimeSpan.FromMinutes(plain);
        }

        if (!Duration.IsMatch(timeString))
            return null;

        long minutes = 0;
        foreach (Match part in DurationPart.Matches(timeString))
        {
            var unit = part.Groups[2].Value.ToLowerInvariant() switch
            {
                "y" => 525960,
                "mo" => 43800,
                "w" => 10080,
                "d" => 1440,
                "h" => 60,
                _ => 1,
            };

            minutes += long.Parse(part.Groups[1].Value) * unit;
        }

        return minutes > MaxMinutes ? null : TimeSpan.FromMinutes(minutes);
    }

    private void SendWarning(string message, Color color)
    {
        SendMessage(new TimeTransferWarningEuiMessage(message, color));
    }

    public override void Opened()
    {
        base.Opened();
        _adminMan.OnPermsChanged += OnPermsChanged;
    }

    public override void Closed()
    {
        base.Closed();
        _adminMan.OnPermsChanged -= OnPermsChanged;
    }

    private void OnPermsChanged(AdminPermsChangedEventArgs args)
    {
        if (args.Player != Player)
            return;

        StateDirty();
    }
}
