using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Shared._Polonium.GameTicking;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.GameWindow;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.GameTicking;

/// <summary>
/// Pushes lobby players to ready up, since game modes are picked from the ready count and a crowd
/// joining after the start skews the round. Reminds players who are not ready before the start,
/// keeps the ready count on the clients' ready button and extends the countdown when too few are ready.
/// </summary>
public sealed partial class LobbyReadinessSystem : EntitySystem
{
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ISharedPlayerManager _players = default!;

    private bool _reminderEnabled;
    private bool _reminderFlash;
    private readonly List<int> _reminderTimes = new();

    private bool _extendEnabled;
    private float _extendRatio;
    private int _extendMinPlayers;
    private int _extendSeconds;
    private int _extendMaxTimes;

    private int _lobbyRoundId = -1;
    private int _extensions;
    private double? _lastSecondsLeft;
    private (int Ready, int Total) _lastCount = (-1, -1);

    public override void Initialize()
    {
        base.Initialize();

        // The countdown has to be extended in the same tick the ticker would preload the maps,
        // before the preset rules get added with the current ready count.
        UpdatesBefore.Add(typeof(GameTicker));

        SubscribeLocalEvent<PlayerJoinedLobbyEvent>(OnPlayerJoinedLobby);

        Subs.CVar(_cfg, CCVars.LobbyReadyReminderEnabled, v => _reminderEnabled = v, true);
        Subs.CVar(_cfg, CCVars.LobbyReadyReminderFlash, v => _reminderFlash = v, true);
        Subs.CVar(_cfg, CCVars.LobbyReadyReminderTimes, ParseReminderTimes, true);
        Subs.CVar(_cfg, CCVars.LobbyExtendEnabled, v => _extendEnabled = v, true);
        Subs.CVar(_cfg, CCVars.LobbyExtendReadyRatio, v => _extendRatio = v, true);
        Subs.CVar(_cfg, CCVars.LobbyExtendMinPlayers, v => _extendMinPlayers = v, true);
        Subs.CVar(_cfg, CCVars.LobbyExtendSeconds, v => _extendSeconds = v, true);
        Subs.CVar(_cfg, CCVars.LobbyExtendMaxTimes, v => _extendMaxTimes = v, true);
    }

    private void ParseReminderTimes(string value)
    {
        _reminderTimes.Clear();

        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(part, out var seconds) && seconds > 0 && !_reminderTimes.Contains(seconds))
                _reminderTimes.Add(seconds);
        }

        _reminderTimes.Sort((a, b) => b.CompareTo(a));
    }

    private void OnPlayerJoinedLobby(PlayerJoinedLobbyEvent ev)
    {
        // Resend the count next tick, the new player has none yet.
        _lastCount = (-1, -1);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_ticker.RunLevel != GameRunLevel.PreRoundLobby || !_ticker.LobbyEnabled)
        {
            _lastSecondsLeft = null;
            return;
        }

        if (_lobbyRoundId != _ticker.RoundId)
        {
            _lobbyRoundId = _ticker.RoundId;
            _extensions = 0;
            _lastSecondsLeft = null;
            _lastCount = (-1, -1);
        }

        var count = CountLobbyPlayers();
        if (count != _lastCount)
        {
            _lastCount = count;
            RaiseNetworkEvent(new LobbyReadyCountEvent(count.Ready, count.Total));
        }

        if (_ticker.Paused || !_ticker.LobbyCountdownRunning)
        {
            _lastSecondsLeft = null;
            return;
        }

        var secondsLeft = (_ticker.LobbyCountdownEnd - _timing.CurTime).TotalSeconds;
        var lastSecondsLeft = _lastSecondsLeft;
        _lastSecondsLeft = secondsLeft;

        // Only act when a threshold is crossed, so a lobby that starts below it does not fire.
        if (lastSecondsLeft is not { } last)
            return;

        if (_reminderEnabled)
        {
            foreach (var time in _reminderTimes)
            {
                if (last <= time || secondsLeft > time)
                    continue;

                RemindNotReady(time);
                break;
            }
        }

        var preload = _ticker.RoundPreloadTime.TotalSeconds;
        if (last > preload && secondsLeft <= preload)
            TryExtendCountdown(count.Ready, count.Total);
    }

    private (int Ready, int Total) CountLobbyPlayers()
    {
        var ready = 0;
        var total = 0;

        foreach (var (userId, status) in _ticker.PlayerGameStatuses)
        {
            if (status == PlayerGameStatus.JoinedGame)
                continue;

            if (!_players.TryGetSessionById(userId, out _))
                continue;

            total++;
            if (status == PlayerGameStatus.ReadyToPlay)
                ready++;
        }

        return (ready, total);
    }

    private void RemindNotReady(int secondsLeft)
    {
        var message = Loc.GetString("lobby-ready-reminder", ("seconds", secondsLeft));

        foreach (var (userId, status) in _ticker.PlayerGameStatuses)
        {
            if (status != PlayerGameStatus.NotReadyToPlay)
                continue;

            if (!_players.TryGetSessionById(userId, out var session))
                continue;

            _chat.DispatchServerMessage(session, message);

            if (_reminderFlash)
                RaiseNetworkEvent(new RequestWindowAttentionEvent(), session.Channel);
        }
    }

    private void TryExtendCountdown(int ready, int total)
    {
        if (!_extendEnabled || _extendSeconds <= 0 || _extensions >= _extendMaxTimes)
            return;

        if (total == 0 || total < _extendMinPlayers)
            return;

        if ((float) ready / total >= _extendRatio)
            return;

        if (!_ticker.DelayStart(TimeSpan.FromSeconds(_extendSeconds), announce: false))
            return;

        _extensions++;
        _chat.DispatchServerAnnouncement(Loc.GetString("lobby-ready-extend-announcement",
            ("seconds", _extendSeconds),
            ("ready", ready),
            ("total", total)));

        Log.Info($"Lobby countdown extended by {_extendSeconds}s, {ready}/{total} players ready");
    }
}
