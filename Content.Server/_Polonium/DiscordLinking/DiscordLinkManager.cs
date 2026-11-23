using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Chat.Managers;
using Content.Server.Database;
using Content.Shared._Polonium.DiscordLinking;
using Content.Shared.CCVar;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.DiscordLinking;

/// <summary>
/// What the bot says about the Discord link of a game account.
/// </summary>
/// <param name="Linked">Whether the account is linked to Discord.</param>
/// <param name="DiscordId">ID of the linked Discord account.</param>
/// <param name="DiscordName">Name of the linked Discord account, e.g. "@nick", if the bot knew it.</param>
/// <param name="RelatedUsers">
/// Game accounts tied to this one by a refused linking attempt, in either direction: accounts whose Discord account
/// this account tried to link, and accounts that tried to link the Discord account of this one.
/// </param>
public sealed record DiscordLinkInfo(
    bool Linked,
    string? DiscordId,
    string? DiscordName,
    IReadOnlyList<NetUserId> RelatedUsers);

/// <summary>
/// Why a connecting player is refused by the Discord link check.
/// </summary>
public readonly record struct DiscordLinkDenial(string Message);

/// <summary>
/// Talks to the Discord bot, which does the OAuth and owns the links between game and Discord accounts.
/// The game keeps no links of its own, only what the bot has last told about the players.
/// </summary>
public sealed partial class DiscordLinkManager : IPostInjectInit
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IHttpClientHolder _http = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IPlayerManager _player = default!;

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    // A connecting player waits for the bot, so the check at connection gives up sooner.
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(5);

    // The connection check has just asked the bot, so the status sent when the player joins can reuse its answer.
    private static readonly TimeSpan JoinStateMaxAge = TimeSpan.FromMinutes(1);

    // An admin alert about the same thing repeats at most this often.
    private static readonly TimeSpan AlertInterval = TimeSpan.FromMinutes(5);

    // What is known about players who are not on the server is forgotten after this long.
    private static readonly TimeSpan CacheMaxAge = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan PruneInterval = TimeSpan.FromMinutes(5);

    private const string DiscordHost = "discord.com";
    private const int MaxDiscordNameLength = 64;
    private static readonly Regex DiscordIdRegex = new(@"^\d{17,20}$", RegexOptions.Compiled);

    private ISawmill _sawmill = default!;

    private readonly Dictionary<NetUserId, (DiscordLinkInfo Info, TimeSpan FetchedAt)> _links = new();
    // Asking about the link and starting linking have separate cooldowns,
    // so the status asked for when the window opens does not hold back the link button.
    private readonly Dictionary<NetUserId, TimeSpan> _nextStatus = new();
    private readonly Dictionary<NetUserId, TimeSpan> _nextStart = new();
    private readonly HashSet<NetUserId> _inFlight = new();
    private readonly Dictionary<NetUserId, string> _denyUrls = new();
    private readonly Dictionary<NetUserId, TimeSpan> _relatedBanAlerts = new();
    private TimeSpan? _lastUnavailableAlert;
    private TimeSpan _nextPrune;

    /// <summary>
    /// Raised when the bot tells something new about the link of an account.
    /// </summary>
    public event Action<NetUserId, DiscordLinkInfo>? LinkChanged;

    /// <summary>
    /// Sends the requests to the bot instead of the shared HTTP client. Tests use it to fake the bot.
    /// </summary>
    internal HttpMessageInvoker? HttpOverride { get; set; }

    private HttpMessageInvoker Http => HttpOverride ?? _http.Client;

    void IPostInjectInit.PostInject()
    {
        _sawmill = _logManager.GetSawmill("discord_link");
    }

    /// <summary>
    /// Whether the account is linked to Discord, as far as the server knows.
    /// The server knows about players on the server and accounts asked about with <see cref="FetchLink"/>.
    /// </summary>
    public bool IsLinked(NetUserId user)
    {
        return TryGetLink(user, out var info) && info.Linked;
    }

    /// <summary>
    /// What the bot last told about the link of the account. False when the server has not asked about it.
    /// </summary>
    public bool TryGetLink(NetUserId user, [NotNullWhen(true)] out DiscordLinkInfo? info)
    {
        info = _links.TryGetValue(user, out var entry) ? entry.Info : null;
        return info != null;
    }

    /// <summary>
    /// Game accounts tied to this one by a refused linking attempt, in either direction, as far as the server knows.
    /// </summary>
    public IReadOnlyList<NetUserId> GetRelatedUsers(NetUserId user)
    {
        return TryGetLink(user, out var info) ? info.RelatedUsers : [];
    }

    /// <summary>
    /// Asks the bot about the link of any account, also of a player who is offline, and remembers the answer.
    /// </summary>
    /// <returns>Null when the bot is not set up or could not be reached.</returns>
    public Task<DiscordLinkInfo?> FetchLink(NetUserId user, CancellationToken cancel = default)
    {
        return FetchLink(user, RequestTimeout, cancel);
    }

    /// <summary>
    /// Answers a player who asks about their Discord link or wants to start linking.
    /// </summary>
    /// <param name="user">The player, as the server knows them from the session.</param>
    /// <param name="name">Name of the player, shown on the confirmation page of the bot.</param>
    /// <param name="login">How the player logged in. Guest accounts can't be linked.</param>
    /// <param name="start">Start linking instead of only asking about the link.</param>
    public async Task<DiscordLinkStateEvent> HandleRequest(NetUserId user, string name, LoginType login, bool start)
    {
        if (!_cfg.GetCVar(CCVars.DiscordLinkEnabled))
            return new DiscordLinkStateEvent(DiscordLinkStatus.Unavailable);

        if (login != LoginType.LoggedIn)
            return new DiscordLinkStateEvent(DiscordLinkStatus.NotLoggedIn);

        var now = _timing.RealTime;
        var cooldowns = start ? _nextStart : _nextStatus;
        if (_inFlight.Contains(user) || cooldowns.TryGetValue(user, out var next) && now < next)
        {
            // Asking about the link too soon is answered with what the server already knows.
            if (!start && TryGetLink(user, out var known))
                return ToState(known);

            return new DiscordLinkStateEvent(DiscordLinkStatus.Cooldown);
        }

        cooldowns[user] = now + TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.DiscordLinkCooldown));
        _inFlight.Add(user);
        try
        {
            if (!start)
            {
                return await FetchLink(user, RequestTimeout, default) is { } info
                    ? ToState(info)
                    : new DiscordLinkStateEvent(DiscordLinkStatus.Unavailable);
            }

            var result = await RequestStart(user, name, RequestTimeout);
            if (result.Linked is { } linked)
                return ToState(linked);

            if (result.Url is not { } url)
                return new DiscordLinkStateEvent(DiscordLinkStatus.Unavailable);

            _sawmill.Info($"{name} ({user}) started linking their Discord account.");
            return new DiscordLinkStateEvent(DiscordLinkStatus.Started, url: url);
        }
        finally
        {
            _inFlight.Remove(user);
        }
    }

    /// <summary>
    /// The link status to tell a player who has just joined, so their client knows which buttons to show.
    /// </summary>
    /// <returns>Null when linking is switched off.</returns>
    public async Task<DiscordLinkStateEvent?> GetJoinState(NetUserId user, LoginType login)
    {
        if (!_cfg.GetCVar(CCVars.DiscordLinkEnabled))
            return null;

        if (login != LoginType.LoggedIn)
            return new DiscordLinkStateEvent(DiscordLinkStatus.NotLoggedIn);

        var now = _timing.RealTime;
        if (_links.TryGetValue(user, out var entry) && now - entry.FetchedAt < JoinStateMaxAge)
            return ToState(entry.Info);

        // The window asks about the link when it opens, this answer will do for that too.
        _nextStatus[user] = now + TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.DiscordLinkCooldown));
        return await FetchLink(user, RequestTimeout, default) is { } info
            ? ToState(info)
            : new DiscordLinkStateEvent(DiscordLinkStatus.Unavailable);
    }

    /// <summary>
    /// Checks a connecting player when a linked Discord account is required to play.
    /// Refuses alt accounts of banned players: accounts tied to a banned account by a refused linking attempt.
    /// </summary>
    /// <returns>Why the player can't connect, or null to let them in.</returns>
    public async Task<DiscordLinkDenial?> CheckConnection(NetUserId user, string name, LoginType login)
    {
        if (!_cfg.GetCVar(CCVars.DiscordLinkEnabled) || !_cfg.GetCVar(CCVars.DiscordLinkRequired))
            return null;

        if (login != LoginType.LoggedIn)
            return new DiscordLinkDenial(Loc.GetString("discord-link-deny-not-logged-in"));

        try
        {
            if (await FetchLink(user, ConnectionTimeout, default) is not { } info)
                return OnCheckFailed();

            if (await FindBannedUser(info.RelatedUsers) is { } banned)
            {
                await AlertRelatedBan(user, name, banned);
                return new DiscordLinkDenial(Loc.GetString("discord-link-deny-related-ban"));
            }

            if (info.Linked)
                return null;

            var start = await RequestStart(user, name, ConnectionTimeout);
            if (start.Linked != null)
                return null;

            if (start.Url is not { } url)
                return OnCheckFailed();

            _denyUrls[user] = url;
            return new DiscordLinkDenial(Loc.GetString("discord-link-deny-not-linked"));
        }
        catch (Exception e)
        {
            _sawmill.Error($"Error while checking the Discord link of connecting player {name} ({user}): {e}");
            return OnCheckFailed();
        }
    }

    /// <summary>
    /// The page where a player refused by <see cref="CheckConnection"/> starts linking. Given out only once,
    /// to the connection denial, so the connecting screen can open it.
    /// </summary>
    public string? TakeDenyUrl(NetUserId user)
    {
        return _denyUrls.Remove(user, out var url) ? url : null;
    }

    /// <summary>
    /// Forgets what is known about a player who has left the server.
    /// </summary>
    public void ForgetPlayer(NetUserId user)
    {
        _links.Remove(user);
        _nextStatus.Remove(user);
        _nextStart.Remove(user);
        _denyUrls.Remove(user);
    }

    private DiscordLinkDenial? OnCheckFailed()
    {
        if (!_cfg.GetCVar(CCVars.DiscordLinkRequiredAllowOnError))
            return new DiscordLinkDenial(Loc.GetString("discord-link-deny-unavailable"));

        var now = _timing.RealTime;
        if (_lastUnavailableAlert is not { } last || now - last >= AlertInterval)
        {
            _lastUnavailableAlert = now;
            _chat.SendAdminAlert(Loc.GetString("discord-link-admin-alert-unavailable"));
        }

        return null;
    }

    private async Task<NetUserId?> FindBannedUser(IReadOnlyList<NetUserId> users)
    {
        foreach (var user in users)
        {
            var bans = await _db.GetBansAsync(null, user, null, null, includeUnbanned: false);
            if (bans.Count > 0)
                return user;
        }

        return null;
    }

    private async Task AlertRelatedBan(NetUserId user, string name, NetUserId banned)
    {
        // A refused player may press reconnect over and over, the admins hear about them once in a while.
        var now = _timing.RealTime;
        if (_relatedBanAlerts.TryGetValue(user, out var last) && now - last < AlertInterval)
            return;

        _relatedBanAlerts[user] = now;

        var record = await _db.GetPlayerRecordByUserId(banned);
        var bannedName = record?.LastSeenUserName ?? banned.ToString();

        _sawmill.Info($"Refused {name} ({user}), whose game account is tied through Discord to banned player {bannedName} ({banned}).");
        _chat.SendAdminAlert(Loc.GetString("discord-link-admin-alert-related-ban",
            ("player", name),
            ("uuid", user.ToString()),
            ("banned", bannedName),
            ("bannedUuid", banned.ToString())));
    }

    private async Task<DiscordLinkInfo?> FetchLink(NetUserId user, TimeSpan timeout, CancellationToken cancel)
    {
        if (!TryGetApi(out var baseUrl, out var token))
            return null;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            cts.CancelAfter(timeout);

            using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/link/status?uuid={user.UserId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await Http.SendAsync(request, cts.Token);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                _sawmill.Warning($"The bot answered {(int) response.StatusCode} when asked about a Discord link.");
                return null;
            }

            if (await response.Content.ReadFromJsonAsync<StatusResponse>(cts.Token) is not { } status)
            {
                _sawmill.Warning("The bot sent an empty answer about a Discord link.");
                return null;
            }

            var info = new DiscordLinkInfo(
                status.Linked,
                status.Linked ? ParseDiscordId(status.DiscordId) : null,
                status.Linked ? ParseDiscordName(status.DiscordName) : null,
                ParseUsers(status.RelatedUuids));

            Remember(user, info);
            return info;
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException or JsonException)
        {
            _sawmill.Warning($"Could not ask the bot about a Discord link: {e.Message}");
            return null;
        }
    }

    private async Task<StartResult> RequestStart(NetUserId user, string name, TimeSpan timeout)
    {
        if (!TryGetApi(out var baseUrl, out var token))
            return default;

        try
        {
            using var cts = new CancellationTokenSource(timeout);

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/link/start");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(new StartRequest(user.UserId.ToString(), name));

            using var response = await Http.SendAsync(request, cts.Token);
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                {
                    var started = await response.Content.ReadFromJsonAsync<StartResponse>(cts.Token);
                    if (started?.Url is { } url && IsDiscordUrl(url))
                        return new StartResult(null, url);

                    // The client opens this link in the browser, so it has to lead where the bot says it does.
                    _sawmill.Warning("The bot sent a linking page that is not on discord.com, ignoring it.");
                    return default;
                }
                case HttpStatusCode.Conflict:
                {
                    var conflict = await response.Content.ReadFromJsonAsync<StartResponse>(cts.Token);
                    if (conflict?.Error != "already_linked")
                        break;

                    var info = new DiscordLinkInfo(
                        true,
                        ParseDiscordId(conflict.DiscordId),
                        ParseDiscordName(conflict.DiscordName),
                        GetRelatedUsers(user));

                    Remember(user, info);
                    return new StartResult(info, null);
                }
            }

            _sawmill.Warning($"The bot answered {(int) response.StatusCode} when asked to start linking.");
            return default;
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException or JsonException)
        {
            _sawmill.Warning($"Could not ask the bot to start linking: {e.Message}");
            return default;
        }
    }

    private void Remember(NetUserId user, DiscordLinkInfo info)
    {
        var now = _timing.RealTime;
        PruneCache(now);

        var changed = !_links.TryGetValue(user, out var old) || !SameLink(old.Info, info);
        _links[user] = (info, now);

        if (changed)
            LinkChanged?.Invoke(user, info);
    }

    /// <summary>
    /// Forgets old answers about players who are not on the server. Players refused at connection never get a session,
    /// so <see cref="ForgetPlayer"/> is never called for them, and <see cref="FetchLink"/> may ask about anyone.
    /// </summary>
    private void PruneCache(TimeSpan now)
    {
        if (now < _nextPrune)
            return;

        _nextPrune = now + PruneInterval;

        var stale = new List<NetUserId>();
        foreach (var (user, entry) in _links)
        {
            if (now - entry.FetchedAt >= CacheMaxAge && !_player.TryGetSessionById(user, out _))
                stale.Add(user);
        }

        foreach (var user in stale)
        {
            _links.Remove(user);
        }

        RemoveOlder(_nextStatus, now);
        RemoveOlder(_nextStart, now);
        RemoveOlder(_relatedBanAlerts, now - AlertInterval);
    }

    private static void RemoveOlder(Dictionary<NetUserId, TimeSpan> times, TimeSpan limit)
    {
        var old = new List<NetUserId>();
        foreach (var (user, time) in times)
        {
            if (time <= limit)
                old.Add(user);
        }

        foreach (var user in old)
        {
            times.Remove(user);
        }
    }

    private static bool SameLink(DiscordLinkInfo a, DiscordLinkInfo b)
    {
        return a.Linked == b.Linked
               && a.DiscordId == b.DiscordId
               && a.DiscordName == b.DiscordName
               && a.RelatedUsers.SequenceEqual(b.RelatedUsers);
    }

    private bool TryGetApi([NotNullWhen(true)] out string? baseUrl, [NotNullWhen(true)] out string? token)
    {
        baseUrl = _cfg.GetCVar(CCVars.DiscordLinkApiUrl).Trim().TrimEnd('/');
        token = _cfg.GetCVar(CCVars.DiscordLinkApiToken).Trim();
        if (baseUrl.Length > 0 && token.Length > 0)
            return true;

        _sawmill.Warning("Discord linking is on, but discord_link.api_url or discord_link.api_token is not set.");
        baseUrl = null;
        token = null;
        return false;
    }

    private static DiscordLinkStateEvent ToState(DiscordLinkInfo info)
    {
        return info.Linked
            ? new DiscordLinkStateEvent(DiscordLinkStatus.Linked, info.DiscordName)
            : new DiscordLinkStateEvent(DiscordLinkStatus.NotLinked);
    }

    private static bool IsDiscordUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
               && uri.Scheme == Uri.UriSchemeHttps
               && uri.Host == DiscordHost;
    }

    private static string? ParseDiscordId(string? id)
    {
        return id != null && DiscordIdRegex.IsMatch(id) ? id : null;
    }

    private static string? ParseDiscordName(string? name)
    {
        return string.IsNullOrWhiteSpace(name) || name.Length > MaxDiscordNameLength ? null : name;
    }

    private List<NetUserId> ParseUsers(List<string>? uuids)
    {
        var users = new List<NetUserId>();
        foreach (var uuid in uuids ?? [])
        {
            if (Guid.TryParse(uuid, out var guid))
                users.Add(new NetUserId(guid));
            else
                _sawmill.Warning($"The bot sent an invalid related UUID: {uuid}");
        }

        return users;
    }

    private readonly record struct StartResult(DiscordLinkInfo? Linked, string? Url);

    private sealed record StartRequest(
        [property: JsonPropertyName("uuid")] string Uuid,
        [property: JsonPropertyName("name")] string Name);

    private sealed class StatusResponse
    {
        [JsonPropertyName("linked")]
        public bool Linked { get; set; }

        [JsonPropertyName("discordId")]
        public string? DiscordId { get; set; }

        [JsonPropertyName("discordName")]
        public string? DiscordName { get; set; }

        [JsonPropertyName("relatedUuids")]
        public List<string>? RelatedUuids { get; set; }
    }

    private sealed class StartResponse
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("discordId")]
        public string? DiscordId { get; set; }

        [JsonPropertyName("discordName")]
        public string? DiscordName { get; set; }
    }
}
