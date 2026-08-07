using System.Threading.Tasks;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Connection;
using Content.Server.Discord.DiscordLink;
using Content.Shared.CCVar;
using Content.Shared.Database;
using NetCord;
using NetCord.Rest;
using Robust.Shared.Configuration;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Color = Robust.Shared.Maths.Color;

namespace Content.Server._Polonium.Chat;

public sealed partial class ShutUpSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IBanManager _bans = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private DiscordLink _discordLink = default!;
    [Dependency] private ILogManager _logManager = default!;

    private ISawmill _sawmill = default!;
    private ulong? _discordChannelId;
    private string[] _fragments = [];
    private bool _enabled;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _logManager.GetSawmill("chat.auto_ban");
        Subs.CVar(_cfg, CCVars.ChatAutoBanEnabled, v => _enabled = v, true);
        Subs.CVar(_cfg, CCVars.ChatAutoBanDiscordChannelId, OnDiscordChannelIdChanged, true);
        Subs.CVar(_cfg, CCVars.ChatAutoBanFragment, OnFragmentsChanged, true);
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
        if (!_enabled || _fragments.Length == 0)
            return false;

        if (!TryFindFragment(message, out var matchedFragment))
            return false;

        var minutes = _cfg.GetCVar(CCVars.ChatAutoBanDurationMinutes);
        var reason = Loc.GetString("chat-auto-ban-reason");

        var banInfo = new CreateServerBanInfo(reason);
        banInfo.AddUser(player.UserId, player.Name);
        banInfo.AddHWId(player.Channel.UserData.GetModernHwid());
        banInfo.AddAddress(player.Channel.RemoteEndPoint.Address);
        banInfo.WithSeverity(NoteSeverity.High);
        banInfo.WithDiscordNotification(false);

        if (minutes > 0)
            banInfo.WithMinutes((uint)minutes);

        _bans.CreateServerBan(banInfo);

        _adminLogger.Add(LogType.AdminMessage, LogImpact.High,
            $"Chat autoban of {player:Player} for message: {message}");

        _ = SendDiscordLog(player.Name, player.UserId.ToString(), message, matchedFragment, minutes);

        return true;
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
                    .WithValue(AsDiscordCode(message)),
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
