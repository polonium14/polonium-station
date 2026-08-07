using System.Threading.Tasks;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Connection;
using Content.Server.Discord.DiscordLink;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using NetCord;
using NetCord.Rest;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Color = Robust.Shared.Maths.Color;

namespace Content.Server._Polonium.Chat;

public sealed partial class ShutUpSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private IBanManager _bans = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private DiscordLink _discordLink = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    private ISawmill _sawmill = default!;
    private ulong? _discordChannelId;
    private string[] _fragments = [];
    private bool _enabled;
    private float _rateLimitPeriod;
    private int _rateLimitCount;

    private readonly Dictionary<NetUserId, (TimeSpan WindowStart, int Count)> _rateCounts = new();
    private readonly HashSet<NetUserId> _handled = new();

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _logManager.GetSawmill("chat.auto_ban");
        Subs.CVar(_cfg, CCVars.ChatAutoBanEnabled, v => _enabled = v, true);
        Subs.CVar(_cfg, CCVars.ChatAutoBanDiscordChannelId, OnDiscordChannelIdChanged, true);
        Subs.CVar(_cfg, CCVars.ChatAutoBanFragment, OnFragmentsChanged, true);
        Subs.CVar(_cfg, CCVars.ChatAutoBanRateLimitPeriod, v => _rateLimitPeriod = v, true);
        Subs.CVar(_cfg, CCVars.ChatAutoBanRateLimitCount, v => _rateLimitCount = v, true);

        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    public override void Shutdown()
    {
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
        base.Shutdown();
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus != SessionStatus.Disconnected)
            return;

        _handled.Remove(e.Session.UserId);
        _rateCounts.Remove(e.Session.UserId);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent _)
    {
        _handled.Clear();
        _rateCounts.Clear();
    }

    private void OnDiscordChannelIdChanged(string channelId)
    {
        if (string.IsNullOrEmpty(channelId))
        {
            _discordChannelId = null;
            return;
        }

        if (ulong.TryParse(channelId, out var id))
        {
            _discordChannelId = id;
            return;
        }

        _discordChannelId = null;
        _sawmill.Error($"Invalid Discord channel ID in {CCVars.ChatAutoBanDiscordChannelId.Name}: '{channelId}'");
    }

    private void OnFragmentsChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            _fragments = [];
            return;
        }

        _fragments = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public bool TryHandle(ICommonSession player, string message)
    {
        if (!_enabled)
            return false;

        if (_handled.Contains(player.UserId))
            return true;

        if (IsRateLimited(player))
        {
            Ban(player, message,
                Loc.GetString("chat-auto-ban-reason-spam"),
                Loc.GetString("chat-auto-ban-discord-fragment-spam",
                    ("count", _rateLimitCount),
                    ("period", _rateLimitPeriod)));
            return true;
        }

        if (_fragments.Length == 0 || !TryFindFragment(message, out var matchedFragment))
            return false;

        Ban(player, message,
            Loc.GetString("chat-auto-ban-reason"),
            matchedFragment);
        return true;
    }

    private bool IsRateLimited(ICommonSession player)
    {
        if (_rateLimitCount <= 0 || _rateLimitPeriod <= 0f)
            return false;

        var now = _timing.CurTime;
        var userId = player.UserId;
        var period = TimeSpan.FromSeconds(_rateLimitPeriod);

        if (!_rateCounts.TryGetValue(userId, out var state) || now - state.WindowStart >= period)
        {
            _rateCounts[userId] = (now, 1);
            return false;
        }

        state.Count++;
        _rateCounts[userId] = state;

        if (state.Count < _rateLimitCount)
            return false;

        _rateCounts.Remove(userId);
        return true;
    }

    private void Ban(ICommonSession player, string message, string reason, string fragment)
    {
        if (_adminManager.IsAdmin(player))
            return;

        _handled.Add(player.UserId);

        var minutes = _cfg.GetCVar(CCVars.ChatAutoBanDurationMinutes);

        var banInfo = new CreateServerBanInfo(reason);
        banInfo.AddUser(player.UserId, player.Name);
        banInfo.AddHWId(player.Channel.UserData.GetModernHwid());
        banInfo.WithSeverity(NoteSeverity.High);
        banInfo.WithDiscordNotification(false);

        if (minutes > 0)
            banInfo.WithMinutes((uint)minutes);

        _bans.CreateServerBan(banInfo);

        _adminLogger.Add(LogType.AdminMessage, LogImpact.High,
            $"Chat autoban of {player:Player} for message: {message}");

        _ = SendDiscordLog(player.Name, player.UserId.ToString(), message, fragment, minutes);
    }

    private bool TryFindFragment(string message, out string matchedFragment)
    {
        foreach (var fragment in _fragments)
        {
            if (message.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                matchedFragment = fragment;
                return true;
            }
        }

        matchedFragment = string.Empty;
        return false;
    }

    private static string AsDiscordCode(string value)
    {
        return $"```\n{value.Replace("`", "'")}\n```";
    }

    private static string TruncateForDiscordCode(string value)
    {
        const int maxContentLength = 508;
        return value.Length <= maxContentLength ? value : value[..maxContentLength];
    }

    private async Task SendDiscordLog(string playerName, string userId, string message, string fragment, int minutes)
    {
        if (_discordChannelId is not { } channelId)
            return;

        var durationText = minutes > 0
            ? Loc.GetString("chat-auto-ban-discord-duration", ("minutes", minutes))
            : Loc.GetString("chat-auto-ban-discord-duration-permanent");

        var safeName = playerName.Replace("`", "'");

        var embed = new EmbedProperties()
            .WithTitle(Loc.GetString("chat-auto-ban-discord-embed-title"))
            .WithColor(new NetCord.Color(Color.Red.ToArgb() & 0xFFFFFF))
            .WithFields(new[]
            {
                new EmbedFieldProperties()
                    .WithName(Loc.GetString("chat-auto-ban-discord-field-player"))
                    .WithValue($"`{safeName}` (`{userId}`)"),
                new EmbedFieldProperties()
                    .WithName(Loc.GetString("chat-auto-ban-discord-field-duration"))
                    .WithValue(durationText),
                new EmbedFieldProperties()
                    .WithName(Loc.GetString("chat-auto-ban-discord-field-fragment"))
                    .WithValue(AsDiscordCode(fragment)),
                new EmbedFieldProperties()
                    .WithName(Loc.GetString("chat-auto-ban-discord-field-message"))
                    .WithValue(AsDiscordCode(TruncateForDiscordCode(message))),
            });

        try
        {
            await _discordLink.SendEmbedAsync(channelId, embed);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to send chat autoban Discord log: {e}");
        }
    }
}
