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

namespace Content.Server.Administration.UI;

public sealed class EntitySearchEui : BaseEui
{
    private const int MaxResults = 300;
    private static readonly TimeSpan SearchCooldown = TimeSpan.FromSeconds(1);

    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    private (string name, NetEntity entity)[] _results = [];
    private TimeSpan _lastSearchTime = TimeSpan.Zero;

    public EntitySearchEui()
    {
        IoCManager.InjectDependencies(this);
    }

    public override EuiStateBase GetNewState()
    {
        return new ToolshedVisualizeEuiState(_results);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is not EntitySearchEuiMsg.Search search)
            return;

        if (!_adminManager.HasAdminFlag(Player, AdminFlags.Admin))
        {
            Close();
            return;
        }

        if (_gameTiming.CurTime - _lastSearchTime < SearchCooldown)
            return;

        _lastSearchTime = _gameTiming.CurTime;
        _results = FindEntities(search.Query);
        StateDirty();
    }

    private (string name, NetEntity entity)[] FindEntities(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var filter = query.Trim();
        var results = new List<(string name, NetEntity entity)>();

        var enumerator = _entities.AllEntityQueryEnumerator<MetaDataComponent>();
        while (enumerator.MoveNext(out var uid, out var meta))
        {
            if (meta.EntityLifeStage >= EntityLifeStage.Deleted)
                continue;

            var protoId = meta.EntityPrototype?.ID;
            var displayName = meta.EntityName;

            if (!MatchesFilter(displayName, protoId, filter))
                continue;

            var label = protoId != null
                ? $"{displayName} ({protoId})"
                : displayName;

            results.Add((label, _entities.GetNetEntity(uid)));

            if (results.Count >= MaxResults)
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
