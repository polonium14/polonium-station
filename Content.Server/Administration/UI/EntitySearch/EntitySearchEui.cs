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

namespace Content.Server.Administration.UI.EntitySearch;

public sealed partial class EntitySearchEui : BaseEui
{
    private const int BatchSize = 300;

    private const int MaxMatchCount = 10_000;

    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _gameTiming = default!;

    private string _query = string.Empty;
    private HashSet<EntityUid>? _gridFilter;
    private List<(string name, string? proto, NetEntity entity)>? _matchCache;
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
                    _gridFilter = ParseGridFilter(search.GridFilterEnabled, search.GridFilter);
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

        (string name, string? proto, NetEntity entity)[] batch;
        if (take == 0)
        {
            batch = [];
        }
        else
        {
            batch = new (string name, string? proto, NetEntity entity)[take];
            cache.CopyTo(_resultsSent, batch, 0, take);
            _resultsSent += take;
        }

        var hasNext = _resultsSent < cache.Count;
        SendMessage(new NewResults(batch, replace, hasNext, cache.Count));
    }

    private List<(string name, string? proto, NetEntity entity)> BuildMatchCache(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var filter = query.Trim();
        var results = new List<(string name, string? proto, NetEntity entity)>();

        var enumerator = _entities.AllEntityQueryEnumerator<MetaDataComponent, TransformComponent>();
        while (enumerator.MoveNext(out var uid, out var meta, out var xform))
        {
            if (meta.EntityLifeStage >= EntityLifeStage.Deleted)
                continue;

            if (_gridFilter != null && (xform.GridUid == null || !_gridFilter.Contains(xform.GridUid.Value)))
                continue;

            var protoId = meta.EntityPrototype?.ID;
            var displayName = meta.EntityName;

            if (!MatchesFilter(displayName, protoId, filter))
                continue;

            var netEntity = _entities.GetNetEntity(uid);

            results.Add((displayName, protoId, netEntity));

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

    // null = no filtering
    private HashSet<EntityUid>? ParseGridFilter(bool enabled, string raw)
    {
        if (!enabled)
            return null;

        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return null;

        var set = new HashSet<EntityUid>();
        foreach (var part in parts)
        {
            if (int.TryParse(part, out var id) && _entities.TryGetEntity(new NetEntity(id), out var uid))
                set.Add(uid.Value);
        }

        return set;
    }

    private static bool MatchesFilter(string displayName, string? protoId, string filter)
    {
        if (displayName.Contains(filter, StringComparison.OrdinalIgnoreCase))
            return true;

        return protoId != null && protoId.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }
}
