// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Chat.Managers;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared.CCVar;
using Content.Shared.Players.PlayTimeTracking;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._Polonium.Administration.Systems;

public sealed partial class NewPlayerAlertSystem : EntitySystem
{
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private PlayTimeTrackingManager _playTime = default!;

    private readonly HashSet<NetUserId> _seen = new();
    private int _threshold;
    private bool _enabled;

    public override void Initialize()
    {
        base.Initialize();

        _playTime.SessionPlayTimeUpdated += OnPlayTimeUpdated;
        _players.PlayerStatusChanged += OnPlayerStatus;

        Subs.CVar(_cfg, CCVars.NewPlayerThreshold, value => _threshold = value, true);
        Subs.CVar(_cfg, CCVars.NewPlayerAlertEnabled, value => _enabled = value, true);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        
        _playTime.SessionPlayTimeUpdated -= OnPlayTimeUpdated;
        _players.PlayerStatusChanged -= OnPlayerStatus;
    }

    private void OnPlayerStatus(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus == SessionStatus.Disconnected)
            _seen.Remove(args.Session.UserId);
    }

    // once per connect, and only after the db time is actually in
    private void OnPlayTimeUpdated(ICommonSession session)
    {
        if (!_enabled || _threshold <= 0 || _seen.Contains(session.UserId))
            return;

        if (!_playTime.TryGetTrackerTimes(session, out var times))
            return;

        _seen.Add(session.UserId);

        times.TryGetValue(PlayTimeTrackingShared.TrackerOverall, out var overall);
        if (overall >= TimeSpan.FromMinutes(_threshold))
            return;

        _chat.SendAdminAlert(Loc.GetString("new-player-admin-alert",
            ("name", session.Name),
            ("hours", (int) overall.TotalHours),
            ("minutes", overall.Minutes)));
    }
}
