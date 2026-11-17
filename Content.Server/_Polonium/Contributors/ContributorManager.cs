using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared.CCVar;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Polonium.Contributors;

/// <summary>
/// Keeps the list of contributors: players whose game account is linked to a Discord account with the contributor role.
/// The Discord bot knows the role and the links, the game fetches the list from its API every few minutes.
/// </summary>
public sealed partial class ContributorManager
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IHttpClientHolder _http = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private IPlayerManager _player = default!;

    // Right after its start the bot answers only once it has the whole member list of the Discord server,
    // which it waits for up to 15 seconds.
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    private const int MinRefreshMinutes = 1;

    // The color goes into chat markup, so only a plain hex color is let through.
    private static readonly Regex ColorRegex = new(@"^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

    private ISawmill _sawmill = default!;

    private HashSet<NetUserId> _contributors = new();
    private string? _color;
    private bool _enabled;
    private TimeSpan _refreshInterval;
    private TimeSpan _nextRefresh;
    private Task? _refresh;

    /// <summary>
    /// Starts following the CVars. Called once at server start.
    /// </summary>
    public void Initialize()
    {
        _sawmill = _logManager.GetSawmill("contributors");

        _cfg.OnValueChanged(CCVars.ContributorsEnabled, OnEnabledChanged, true);
        _cfg.OnValueChanged(CCVars.ContributorsRefreshMinutes, OnRefreshMinutesChanged, true);
    }

    /// <summary>
    /// Fetches the list again when it is time. Called every frame.
    /// </summary>
    public void Update()
    {
        if (!_enabled || _timing.RealTime < _nextRefresh || _refresh is { IsCompleted: false })
            return;

        _nextRefresh = _timing.RealTime + _refreshInterval;
        // The fetch never throws, it is kept only to know whether it is still running.
        _refresh = Fetch();
    }

    /// <summary>
    /// Whether the account is linked to a Discord account with the contributor role, as of the last fetch.
    /// </summary>
    public bool IsContributor(NetUserId user)
    {
        return _contributors.Contains(user);
    }

    /// <summary>
    /// Whether the player is linked to a Discord account with the contributor role, as of the last fetch.
    /// </summary>
    public bool IsContributor(ICommonSession player)
    {
        return IsContributor(player.UserId);
    }

    /// <summary>
    /// Whether the player controlling the entity is a contributor. False when no player controls it.
    /// </summary>
    public bool IsContributor(EntityUid uid)
    {
        return _player.TryGetSessionByEntity(uid, out var session) && IsContributor(session);
    }

    /// <summary>
    /// The color of the contributor role on Discord, if the player is a contributor and the role has a color.
    /// </summary>
    public bool TryGetNameColor(NetUserId user, [NotNullWhen(true)] out string? color)
    {
        color = _color != null && _contributors.Contains(user) ? _color : null;
        return color != null;
    }

    private void OnEnabledChanged(bool enabled)
    {
        _enabled = enabled;
        if (enabled)
        {
            _nextRefresh = TimeSpan.Zero;
            return;
        }

        _contributors.Clear();
        _color = null;
    }

    private void OnRefreshMinutesChanged(int minutes)
    {
        _refreshInterval = TimeSpan.FromMinutes(Math.Max(minutes, MinRefreshMinutes));
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
                _sawmill.Warning($"The bot answered {(int) response.StatusCode} when asked for contributors, keeping the previous list.");
                return;
            }

            var answer = await response.Content.ReadFromJsonAsync<ContributorsResponse>(cts.Token);
            if (answer?.Contributors is not { } entries)
            {
                _sawmill.Warning("The bot sent no contributor list, keeping the previous list.");
                return;
            }

            // Switched off while the bot was answering.
            if (!_enabled)
                return;

            var contributors = new HashSet<NetUserId>();
            foreach (var entry in entries)
            {
                // Contributors who have not linked their game account come without a UUID.
                if (entry?.Uuid is not { } uuid)
                    continue;

                if (Guid.TryParse(uuid, out var guid))
                    contributors.Add(new NetUserId(guid));
                else
                    _sawmill.Warning($"The bot sent an invalid contributor UUID: {uuid}");
            }

            var color = ParseColor(answer.Color);
            if (color != _color || !contributors.SetEquals(_contributors))
                _sawmill.Info($"Contributors changed: {contributors.Count} linked contributors, name color {color ?? "none"}.");

            _contributors = contributors;
            _color = color;
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException or JsonException)
        {
            _sawmill.Warning($"Could not fetch contributors from the bot, keeping the previous list: {e.Message}");
        }
        catch (Exception e)
        {
            // Nothing awaits the fetch, so an exception must not get out of it.
            _sawmill.Error($"Error while fetching contributors from the bot: {e}");
        }
    }

    private string? ParseColor(string? color)
    {
        // A role without a color.
        if (color == null)
            return null;

        if (ColorRegex.IsMatch(color))
            return color;

        _sawmill.Warning($"The bot sent an invalid contributor color: {color}");
        return null;
    }

    private bool TryGetApi([NotNullWhen(true)] out string? url, [NotNullWhen(true)] out string? token)
    {
        url = _cfg.GetCVar(CCVars.ContributorsApiUrl).Trim();
        // The same bot as for Discord links, so the same token.
        token = _cfg.GetCVar(CCVars.DiscordLinkApiToken).Trim();
        if (url.Length > 0 && token.Length > 0)
            return true;

        _sawmill.Warning("Contributors are on, but contributors.api_url or discord_link.api_token is not set.");
        url = null;
        token = null;
        return false;
    }

    private sealed class ContributorsResponse
    {
        [JsonPropertyName("color")]
        public string? Color { get; set; }

        [JsonPropertyName("contributors")]
        public List<ContributorEntry?>? Contributors { get; set; }
    }

    private sealed class ContributorEntry
    {
        [JsonPropertyName("uuid")]
        public string? Uuid { get; set; }
    }
}
