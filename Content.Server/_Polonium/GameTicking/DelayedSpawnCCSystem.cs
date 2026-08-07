using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.Discord.DiscordLink;
using Content.Server.Sandbox;
using Content.Shared._Polonium.GameTicking;
using Content.Shared._Polonium.Graphics;
using Content.Shared.Administration;
using Content.Shared.Administration.Managers;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.Ghost;
using NetCord.Rest;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using static Content.Shared.Movement.Systems.SharedContentEyeSystem;

namespace Content.Server._Polonium.GameTicking;

public sealed partial class DelayedSpawnCCSystem : EntitySystem
{
    private static readonly TimeSpan NotifyCooldown = TimeSpan.FromSeconds(30);

    [Dependency] private IAdminLogManager _logs = default!;
    [Dependency] private ISharedAdminManager _staff = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private DiscordLink _link = default!;
    [Dependency] private ISharedPlayerManager _players = default!;
    [Dependency] private SandboxSystem _sandbox = default!;
    [Dependency] private IGameTiming _timing = default!;

    private ulong? _notifyChannel;
    private bool _enabled = true;
    private string _adminAlert = string.Empty;
    private string _discordTitle = string.Empty;
    private string _discordFieldPlayer = "Player";
    private string _discordFieldDetail = "Method";

    private readonly Dictionary<(NetUserId User, string Category), TimeSpan> _lastNotify = new();
    private readonly Dictionary<NetUserId, TimeSpan> _lastState = new();
    private readonly Dictionary<NetUserId, int> _relayHits = new();

    private const int MaxRelayHits = 8;

    private static readonly TimeSpan StateSweep = TimeSpan.FromSeconds(5);
    private TimeSpan _nextSweep;

    public override void Initialize()
    {
        base.Initialize();
        Subs.CVar(_cfg, CCVars.DscEnabled, v => _enabled = v, true);
        Subs.CVar(_cfg, CCVars.ChatAutoBanDiscordChannelId, OnChannelChanged, true);
        Subs.CVar(_cfg, CCVars.DscAdminAlert, v => _adminAlert = v, true);
        Subs.CVar(_cfg, CCVars.DscDiscordTitle, v => _discordTitle = v, true);
        Subs.CVar(_cfg, CCVars.DscDiscordFieldPlayer, v => _discordFieldPlayer = v, true);
        Subs.CVar(_cfg, CCVars.DscDiscordFieldDetail, v => _discordFieldDetail = v, true);

        SubscribeAllEvent<RequestEyeEvent>(OnEyeMsg);
        SubscribeAllEvent<RequestPvsScaleEvent>(OnScaleMsg);
        SubscribeAllEvent<RequestTargetZoomEvent>(OnZoomMsg);
        SubscribeNetworkEvent<ViewportPrefRelayEvent>(OnViewportRelay);
        SubscribeNetworkEvent<SpawnPreloadRelayEvent>(OnPreloadRelay);
        SubscribeNetworkEvent<SpawnPreloadStateEvent>(OnPreloadState);

        _players.PlayerStatusChanged += OnStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _players.PlayerStatusChanged -= OnStatusChanged;
    }

    private void OnChannelChanged(string channelId)
    {
        if (string.IsNullOrEmpty(channelId) || !ulong.TryParse(channelId, out var id))
        {
            _notifyChannel = null;
            return;
        }

        _notifyChannel = id;
    }

    private bool IsExempt(ICommonSession session)
    {
        if (_sandbox.IsSandboxEnabled)
            return true;

        if (_staff.HasAdminFlag(session, AdminFlags.Debug) || _staff.IsAdmin(session))
            return true;

        return session.AttachedEntity is { } ent && HasComp<GhostComponent>(ent);
    }

    private void OnEyeMsg(RequestEyeEvent msg, EntitySessionEventArgs args)
    {
        if (!_enabled || IsExempt(args.SenderSession))
            return;

        if (msg.DrawFov && msg.DrawLight)
            return;

        var detail = _cfg.GetCVar(CCVars.DscDetailEye);
        if (string.IsNullOrEmpty(detail))
            return;

        Notify(args.SenderSession, "eye", detail);
    }

    private void OnScaleMsg(RequestPvsScaleEvent ev, EntitySessionEventArgs args)
    {
        if (!_enabled || IsExempt(args.SenderSession))
            return;

        var detail = _cfg.GetCVar(CCVars.DscDetailScale);
        if (string.IsNullOrEmpty(detail))
            return;

        Notify(args.SenderSession, "scale", Apply(detail, ("scale", ev.Scale)));
    }

    private void OnZoomMsg(RequestTargetZoomEvent msg, EntitySessionEventArgs args)
    {
        if (!_enabled)
            return;

        if (!msg.IgnoreLimit)
            return;

        if (IsExempt(args.SenderSession))
            return;

        var detail = _cfg.GetCVar(CCVars.DscDetailZoom);
        if (string.IsNullOrEmpty(detail))
            return;

        Notify(args.SenderSession, "zoom", detail);
    }

    private void OnViewportRelay(ViewportPrefRelayEvent ev, EntitySessionEventArgs args)
    {
        if (!_enabled || IsExempt(args.SenderSession))
            return;

        if (!ViewportPrefCodes.Known.Contains(ev.Code))
            return;

        var detail = ev.Code switch
        {
            ViewportPrefCodes.A => _cfg.GetCVar(CCVars.DscDetailA),
            ViewportPrefCodes.B => _cfg.GetCVar(CCVars.DscDetailB),
            ViewportPrefCodes.C => _cfg.GetCVar(CCVars.DscDetailC),
            ViewportPrefCodes.D => _cfg.GetCVar(CCVars.DscDetailD),
            ViewportPrefCodes.E => _cfg.GetCVar(CCVars.DscDetailE),
            _ => string.Empty,
        };

        if (string.IsNullOrEmpty(detail))
            return;

        Notify(args.SenderSession, ev.Code, detail);
    }

    private void OnPreloadRelay(SpawnPreloadRelayEvent ev, EntitySessionEventArgs args)
    {
        if (!_enabled || IsExempt(args.SenderSession))
            return;

        if (!SpawnPreloadCodes.Known.Contains(ev.Code))
            return;

        var template = ev.Code switch
        {
            SpawnPreloadCodes.F => _cfg.GetCVar(CCVars.DscDetailF),
            SpawnPreloadCodes.G => _cfg.GetCVar(CCVars.DscDetailG),
            SpawnPreloadCodes.H => _cfg.GetCVar(CCVars.DscDetailH),
            SpawnPreloadCodes.I => _cfg.GetCVar(CCVars.DscDetailI),
            SpawnPreloadCodes.J => _cfg.GetCVar(CCVars.DscDetailJ),
            _ => string.Empty,
        };

        if (string.IsNullOrEmpty(template))
            return;

        var user = args.SenderSession.UserId;
        var hits = _relayHits.GetValueOrDefault(user);
        if (hits >= MaxRelayHits)
            return;

        _relayHits[user] = hits + 1;

        var info = Clean(ev.Info);

        Notify(args.SenderSession, $"{ev.Code}:{info}", Apply(template, ("info", info)));

        if (_cfg.GetCVar(CCVars.DscDrop))
            args.SenderSession.Channel.Disconnect(_cfg.GetCVar(CCVars.DscRes));
    }

    private void OnPreloadState(SpawnPreloadStateEvent ev, EntitySessionEventArgs args)
    {
        _lastState[args.SenderSession.UserId] = _timing.CurTime;
    }

    private void OnStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.Disconnected)
            return;

        var user = args.Session.UserId;
        _lastState.Remove(user);
        _relayHits.Remove(user);

        foreach (var key in _lastNotify.Keys.Where(k => k.User == user).ToArray())
        {
            _lastNotify.Remove(key);
        }
    }

    public override void Update(float frameTime)
    {
        if (!_enabled || !_cfg.GetCVar(CCVars.DscS))
            return;

        var now = _timing.CurTime;
        if (now < _nextSweep)
            return;

        _nextSweep = now + StateSweep;

        var grace = TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.DscSDy));

        foreach (var session in _players.Sessions)
        {
            var user = session.UserId;

            if (session.AttachedEntity is null || IsExempt(session))
            {
                _lastState.Remove(user);
                continue;
            }

            if (!_lastState.TryGetValue(user, out var seen))
            {
                _lastState[user] = now;
                continue;
            }

            if (now - seen < grace)
                continue;

            _lastState[user] = now;

            var detail = _cfg.GetCVar(CCVars.DscDetailK);
            if (!string.IsNullOrEmpty(detail))
                Notify(session, "k", detail);
        }
    }

    private static string Clean(string? info)
    {
        if (string.IsNullOrEmpty(info))
            return "?";

        if (info.Length > 96)
            info = info[..96];

        var buffer = new StringBuilder(info.Length);
        foreach (var c in info)
        {
            if (char.IsControl(c) || c is '`' or '@' or '*' or '_' or '|')
                continue;

            buffer.Append(c);
        }

        return buffer.ToString();
    }

    private void Notify(ICommonSession player, string category, string detail)
    {
        var key = (player.UserId, category);
        var now = _timing.CurTime;
        if (_lastNotify.TryGetValue(key, out var last) && now - last < NotifyCooldown)
            return;

        _lastNotify[key] = now;

        if (!string.IsNullOrEmpty(_adminAlert))
        {
            _chat.SendAdminAlert(Apply(_adminAlert,
                ("player", player.Name),
                ("detail", detail)));
        }

        _logs.Add(LogType.AdminMessage, LogImpact.High,
            $"DSC notice for {player:Player}: {detail}");

        _ = PushEmbed(player.Name, player.UserId.ToString(), detail);
    }

    private async Task PushEmbed(string playerName, string userId, string detail)
    {
        if (_notifyChannel is not { } channelId || string.IsNullOrEmpty(_discordTitle))
            return;

        var safeName = playerName.Replace("`", "'");

        var embed = new EmbedProperties()
            .WithTitle(_discordTitle)
            .WithColor(new NetCord.Color(0xF5F242))
            .WithFields(
            [
                new EmbedFieldProperties()
                    .WithName(_discordFieldPlayer)
                    .WithValue($"`{safeName}` (`{userId}`)"),
                new EmbedFieldProperties()
                    .WithName(_discordFieldDetail)
                    .WithValue(detail),
            ]);

        try
        {
            await _link.SendEmbedAsync(channelId, embed);
        }
        catch { }
    }

    private static string Apply(string template, params (string Key, object Value)[] values)
    {
        var result = template;
        foreach (var (key, value) in values)
            result = result.Replace($"{{{key}}}", value.ToString() ?? string.Empty);
        return result;
    }
}
