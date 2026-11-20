using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Content.Server._Polonium.DiscordLinking;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.PlayerLists;

public abstract partial class PlayerListManager
{
    [Dependency] protected IConfigurationManager Cfg = default!;
    [Dependency] private IHttpClientHolder _http = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private DiscordLinkManager _links = default!;

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    private const int MinRetrySeconds = 1;

    private static readonly Regex ColorRegex = new(@"^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

    private ISawmill _sawmill = default!;

    private HashSet<NetUserId> _players = new();
    private bool _enabled;
    private TimeSpan _nextFetch;
    private TimeSpan _lastFetch;
    private Task? _fetch;

    /// <summary>
    /// Name of the list in logs and the key of the array in the bot's answer.
    /// </summary>
    protected abstract string Name { get; }

    protected abstract CVarDef<bool> EnabledCVar { get; }

    protected abstract CVarDef<string> ApiUrlCVar { get; }

    protected abstract CVarDef<string> ApiTokenCVar { get; }

    protected abstract CVarDef<int> FetchCooldownCVar { get; }

    protected abstract CVarDef<int> RetryDelayCVar { get; }

    /// <summary>
    /// How often the list is fetched when nothing else asks for it. Null to fetch only when asked.
    /// </summary>
    protected abstract TimeSpan? RefreshInterval { get; }

    /// <summary>
    /// The color of the names of listed players in chat, or null to leave them as they are.
    /// </summary>
    protected abstract string? NameColor { get; }

    /// <summary>
    /// The color of the Discord role that the bot sent with the last list.
    /// </summary>
    protected string? BotColor { get; private set; }

    /// <summary>
    /// Starts following the CVars and the Discord links. Called once at server start.
    /// </summary>
    public void Initialize()
    {
        _sawmill = _logManager.GetSawmill(Name);

        Cfg.OnValueChanged(EnabledCVar, OnEnabledChanged, true);
        _links.LinkChanged += OnLinkChanged;
    }

    /// <summary>
    /// Fetches the list again when it is time. Called every frame.
    /// </summary>
    public void Update()
    {
        if (!_enabled || _timing.RealTime < _nextFetch || _fetch is { IsCompleted: false })
            return;

        _lastFetch = _timing.RealTime;
        _nextFetch = RefreshInterval is { } interval ? _lastFetch + interval : TimeSpan.MaxValue;
        // The fetch never throws, it is kept only to know whether it is still running.
        _fetch = Fetch();
    }

    /// <summary>
    /// Whether the account is on the list, as of the last fetch.
    /// </summary>
    public bool Contains(NetUserId user)
    {
        return _players.Contains(user);
    }

    /// <summary>
    /// Whether the player is on the list, as of the last fetch.
    /// </summary>
    public bool Contains(ICommonSession player)
    {
        return Contains(player.UserId);
    }

    public bool TryGetNameColor(NetUserId user, [NotNullWhen(true)] out string? color)
    {
        color = Contains(user) ? NameColor : null;
        return color != null;
    }

    private void OnEnabledChanged(bool enabled)
    {
        _enabled = enabled;
        if (enabled)
        {
            _nextFetch = TimeSpan.Zero;
            return;
        }

        _players.Clear();
        BotColor = null;
    }

    private void OnLinkChanged(NetUserId user, DiscordLinkInfo info)
    {
        FetchBy(_lastFetch + TimeSpan.FromSeconds(Math.Max(Cfg.GetCVar(FetchCooldownCVar), 0)));
    }

    private void RetryLater()
    {
        FetchBy(_timing.RealTime + TimeSpan.FromSeconds(Math.Max(Cfg.GetCVar(RetryDelayCVar), MinRetrySeconds)));
    }

    private void FetchBy(TimeSpan time)
    {
        if (time < _nextFetch)
            _nextFetch = time;
    }

    private async Task Fetch()
    {
        if (!TryGetApi(out var url, out var token))
            return;

        try
        {
            using var cts = new CancellationTokenSource(RequestTimeout);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _http.Client.SendAsync(request, cts.Token);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                _sawmill.Warning($"The bot answered {(int) response.StatusCode} when asked for {Name}, keeping the previous list.");
                RetryLater();
                return;
            }

            var answer = await response.Content.ReadFromJsonAsync<JsonElement>(cts.Token);
            if (answer.ValueKind != JsonValueKind.Object
                || !answer.TryGetProperty(Name, out var entries)
                || entries.ValueKind != JsonValueKind.Array)
            {
                _sawmill.Warning($"The bot sent no {Name} list, keeping the previous list.");
                RetryLater();
                return;
            }

            if (!_enabled)
                return;

            var players = ParsePlayers(entries);
            var color = answer.TryGetProperty("color", out var colorElement) && colorElement.ValueKind == JsonValueKind.String
                ? ParseColor(colorElement.GetString())
                : null;

            if (color != BotColor || !players.SetEquals(_players))
                _sawmill.Info($"The {Name} list changed: {players.Count} linked players, role color {color ?? "none"}.");

            _players = players;
            BotColor = color;
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException or JsonException)
        {
            _sawmill.Warning($"Could not fetch {Name} from the bot, keeping the previous list: {e.Message}");
            RetryLater();
        }
        catch (Exception e)
        {
            _sawmill.Error($"Error while fetching {Name} from the bot: {e}");
            RetryLater();
        }
    }

    private HashSet<NetUserId> ParsePlayers(JsonElement entries)
    {
        var players = new HashSet<NetUserId>();
        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object
                || !entry.TryGetProperty("uuid", out var uuid)
                || uuid.ValueKind != JsonValueKind.String)
                continue;

            if (entry.TryGetProperty("active", out var active) && active.ValueKind == JsonValueKind.False)
                continue;

            if (Guid.TryParse(uuid.GetString(), out var guid))
                players.Add(new NetUserId(guid));
            else
                _sawmill.Warning($"The bot sent an invalid UUID in the {Name} list: {uuid.GetString()}");
        }

        return players;
    }

    private string? ParseColor(string? color)
    {
        if (color == null)
            return null;

        if (ColorRegex.IsMatch(color))
            return color;

        _sawmill.Warning($"The bot sent an invalid {Name} role color: {color}");
        return null;
    }

    private bool TryGetApi([NotNullWhen(true)] out string? url, [NotNullWhen(true)] out string? token)
    {
        url = Cfg.GetCVar(ApiUrlCVar).Trim();
        token = Cfg.GetCVar(ApiTokenCVar).Trim();
        if (url.Length > 0 && token.Length > 0)
            return true;

        _sawmill.Warning($"The {Name} list is on, but {ApiUrlCVar.Name} or {ApiTokenCVar.Name} is not set.");
        url = null;
        token = null;
        return false;
    }
}
