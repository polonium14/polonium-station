// SPDX-FileCopyrightText: 2025 Polonium Station Contributors
//
// SPDX-License-Identifier: MIT

using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared.Administration;
using Content.Shared.Bql;
using Content.Shared.Eui;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Timing;
using static Content.Shared.Administration.EntitySearchEuiMsg;

namespace Content.Server.Administration.UI;

public sealed class EntitySearchEui : BaseEui
{
    private const int BatchSize = 300;
    private static readonly TimeSpan SearchCooldown = TimeSpan.FromSeconds(3);

    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    private string _query = string.Empty;
    private int _resultsSent;
    private TimeSpan _lastSearchTime = TimeSpan.Zero;

    public EntitySearchEui()
    {
        IoCManager.InjectDependencies(this);
    }

    public override EuiStateBase GetNewState()
    {
        return new ToolshedVisualizeEuiState([]);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (!_adminManager.HasAdminFlag(Player, AdminFlags.Admin))
        {
            Close();
            return;
        }

        switch (msg)
        {
            case Search search:
            {
                if (_gameTiming.CurTime - _lastSearchTime < SearchCooldown)
                    return;

                _lastSearchTime = _gameTiming.CurTime;
                _query = search.Query.Trim();
                SendResults(replace: true);
                break;
            }
            case NextResultsRequest:
                SendResults(replace: false);
                break;
        }
    }

    private void SendResults(bool replace)
    {
        if (replace)
            _resultsSent = 0;

        var results = FindEntities(_query, _resultsSent, BatchSize);

        if (results.Length > 0)
            _resultsSent += results.Length;

        var hasNext = results.Length >= BatchSize;
        SendMessage(new NewResults(results, replace, hasNext));
    }

    private (string name, NetEntity entity)[] FindEntities(string query, int skipMatches, int limit)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var filter = query.Trim();
        var results = new List<(string name, NetEntity entity)>();
        var skipped = 0;

        var enumerator = _entities.AllEntityQueryEnumerator<MetaDataComponent>();
        while (enumerator.MoveNext(out var uid, out var meta))
        {
            if (meta.EntityLifeStage >= EntityLifeStage.Deleted)
                continue;

            var protoId = meta.EntityPrototype?.ID;
            var displayName = meta.EntityName;

            if (!MatchesFilter(displayName, protoId, filter))
                continue;

            if (skipped < skipMatches)
            {
                skipped++;
                continue;
            }

            var label = protoId != null
                ? $"{displayName} ({protoId})"
                : displayName;

            results.Add((label, _entities.GetNetEntity(uid)));

            if (results.Count >= limit)
                break;
        }

        return results.ToArray();
    }

    private static bool MatchesFilter(string displayName, string? protoId, string filter)
    {
        if (displayName.Contains(filter, StringComparison.OrdinalIgnoreCase))
            return true;

        return protoId != null && protoId.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }
}
