using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.EUI;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.Eui;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Timing;
using static Content.Shared.Administration.EntitySearchEuiMsg;

namespace Content.Server.Administration.UI;

public sealed class EntitySearchEui : BaseEui
{
    private const int BatchSize = 300;

    private const int MaxMatchCount = 10_000;

    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    private string _query = string.Empty;
    private List<(string name, NetEntity entity)>? _matchCache;
    private int _resultsSent;
    private TimeSpan _lastSearchTime = TimeSpan.Zero;

    public EntitySearchEui(IDependencyCollection deps)
    {
        deps.InjectDependencies(this, oneOff: true);
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
                    if (_gameTiming.CurTime - _lastSearchTime < EntitySearchEuiMsg.SearchCooldown)
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
        {
            _resultsSent = 0;
            _matchCache = BuildMatchCache(_query);
            TryLogExpensiveSearch(_matchCache.Count);
        }
        else if (_matchCache == null)
        {
            _matchCache = BuildMatchCache(_query);
        }

        var cache = _matchCache;
        var remaining = cache.Count - _resultsSent;
        var take = Math.Min(BatchSize, remaining);

        (string name, NetEntity entity)[] batch;
        if (take == 0)
        {
            batch = [];
        }
        else
        {
            batch = new (string name, NetEntity entity)[take];
            cache.CopyTo(_resultsSent, batch, 0, take);
            _resultsSent += take;
        }

        var hasNext = _resultsSent < cache.Count;
        SendMessage(new NewResults(batch, replace, hasNext));
    }

    private List<(string name, NetEntity entity)> BuildMatchCache(string query)
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

            var netEntity = _entities.GetNetEntity(uid);

            var label = protoId != null
                ? $"{displayName} ({protoId}) [{netEntity}]"
                : $"{displayName} [{netEntity}]";

            results.Add((label, netEntity));

            if (results.Count >= MaxMatchCount)
                break;
        }

        return results;
    }

    private void TryLogExpensiveSearch(int resultCount)
    {
        var threshold = _cfg.GetCVar(CCVars.EntitySearchLogMinResults);

        if (threshold <= 0)
            return;

        if (resultCount < threshold)
            return;

        var message = Loc.GetString("admin-entity-search-log",
            ("admin", Player.Name),
            ("count", resultCount));

        _chat.SendAdminAlert(message);
        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{Player.Name} ran entity search and got {resultCount} results.");
    }

    private static bool MatchesFilter(string displayName, string? protoId, string filter)
    {
        if (displayName.Contains(filter, StringComparison.OrdinalIgnoreCase))
            return true;

        return protoId != null && protoId.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }
}
