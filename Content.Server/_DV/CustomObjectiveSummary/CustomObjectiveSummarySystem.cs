// SPDX-FileCopyrightText: 2025 beck-thompson <107373427+beck-thompson@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration.Logs;
using Content.Server.Players.RateLimiting;
using Content.Shared._DV.CustomObjectiveSummary;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.Mind;
using Content.Shared.Players.RateLimiting;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._DV.CustomObjectiveSummary;

public sealed partial class CustomObjectiveSummarySystem : EntitySystem
{
    [Dependency] private IServerNetManager _net = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private PlayerRateLimitManager _rateLimit = default!;

    private const string RateLimitKey = "ObjectiveSummary";

    public override void Initialize()
    {
        SubscribeLocalEvent<EvacShuttleLeftEvent>(OnEvacShuttleLeft);

        _net.RegisterNetMessage<CustomObjectiveClientSetObjective>(OnCustomObjectiveFeedback);

        _rateLimit.Register(RateLimitKey,
            new RateLimitRegistration(CCVars.ObjectiveSummaryRateLimitPeriod,
                CCVars.ObjectiveSummaryRateLimitCount,
                null));
    }

    private void OnCustomObjectiveFeedback(CustomObjectiveClientSetObjective msg)
    {
        if (!_player.TryGetSessionById(msg.MsgChannel.UserId, out var session))
            return;

        // Rate limit to stop a scripted client from spamming submissions (and admin-log lines).
        if (_rateLimit.CountAction(session, RateLimitKey) != RateLimitStatus.Allowed)
            return;

        if (!_mind.TryGetMind(msg.MsgChannel.UserId, out var mind))
            return;

        if (mind.Value.Comp.Objectives.Count == 0)
            return;

        // The client caps this too, but never trust the client: a modified client can send an
        // arbitrarily large string, which we store, network, admin-log, and word-wrap at round end.
        var summary = msg.Summary.Length > CustomObjectiveSummaryComponent.MaxSummaryLength
            ? msg.Summary[..CustomObjectiveSummaryComponent.MaxSummaryLength]
            : msg.Summary;

        var comp = EnsureComp<CustomObjectiveSummaryComponent>(mind.Value);

        comp.ObjectiveSummary = summary;
        Dirty(mind.Value.Owner, comp);

        _adminLog.Add(LogType.ObjectiveSummary, $"{ToPrettyString(mind.Value.Comp.OwnedEntity)} wrote objective summery: {summary}");
    }

    private void OnEvacShuttleLeft(EvacShuttleLeftEvent args)
    {
        var allMinds = _mind.GetAliveHumans();

        // Assumes the assistant is still there at the end of the round.
        foreach (var mind in allMinds)
        {
            // Only send the popup to people with objectives.
            if (mind.Comp.Objectives.Count == 0)
                continue;

            if (mind.Comp.UserId is not { } userId || !_player.TryGetSessionById(userId, out var session))
                continue;

            RaiseNetworkEvent(new CustomObjectiveSummaryOpenMessage(), session);
        }
    }
}
