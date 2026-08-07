using System.Threading.Tasks;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.Discord.DiscordLink;
using Content.Server.Sandbox;
using Content.Shared._Polonium.Graphics;
using Content.Shared.Administration;
using Content.Shared.Administration.Managers;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.Ghost;
using NetCord.Rest;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using static Content.Shared.Movement.Systems.SharedContentEyeSystem;

namespace Content.Server._Polonium.GameTicking;

public sealed partial class DelayedSpawnCCSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _logs = default!;
    [Dependency] private ISharedAdminManager _staff = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private DiscordLink _link = default!;
    [Dependency] private SandboxSystem _sandbox = default!;

    private ulong? _notifyChannel;
    private string _adminAlert = string.Empty;
    private string _discordTitle = string.Empty;
    private string _discordFieldPlayer = "Player";
    private string _discordFieldDetail = "Method";

    public override void Initialize()
    {
        base.Initialize();
        Subs.CVar(_cfg, CCVars.ChatAutoBanDiscordChannelId, OnChannelChanged, true);
        Subs.CVar(_cfg, CCVars.DscAdminAlert, v => _adminAlert = v, true);
        Subs.CVar(_cfg, CCVars.DscDiscordTitle, v => _discordTitle = v, true);
        Subs.CVar(_cfg, CCVars.DscDiscordFieldPlayer, v => _discordFieldPlayer = v, true);
        Subs.CVar(_cfg, CCVars.DscDiscordFieldDetail, v => _discordFieldDetail = v, true);

        SubscribeAllEvent<RequestEyeEvent>(OnEyeMsg);
        SubscribeAllEvent<RequestPvsScaleEvent>(OnScaleMsg);
        SubscribeAllEvent<RequestTargetZoomEvent>(OnZoomMsg);
        SubscribeNetworkEvent<ViewportPrefRelayEvent>(OnViewportRelay);
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

    private void OnEyeMsg(RequestEyeEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } player)
            return;

        if (_sandbox.IsSandboxEnabled || HasComp<GhostComponent>(player) || _staff.IsAdmin(args.SenderSession))
            return;

        if (msg.DrawFov && msg.DrawLight)
            return;

        var detail = _cfg.GetCVar(CCVars.DscDetailEye);
        if (string.IsNullOrEmpty(detail))
            return;

        Notify(args.SenderSession, detail);
    }

    private void OnScaleMsg(RequestPvsScaleEvent ev, EntitySessionEventArgs args)
    {
        if (_sandbox.IsSandboxEnabled || _staff.HasAdminFlag(args.SenderSession, AdminFlags.Debug))
            return;

        var detail = _cfg.GetCVar(CCVars.DscDetailScale);
        if (string.IsNullOrEmpty(detail))
            return;

        Notify(args.SenderSession, Apply(detail, ("scale", ev.Scale)));
    }

    private void OnZoomMsg(RequestTargetZoomEvent msg, EntitySessionEventArgs args)
    {
        if (!msg.IgnoreLimit)
            return;

        if (_sandbox.IsSandboxEnabled || _staff.HasAdminFlag(args.SenderSession, AdminFlags.Debug))
            return;

        var detail = _cfg.GetCVar(CCVars.DscDetailZoom);
        if (string.IsNullOrEmpty(detail))
            return;

        Notify(args.SenderSession, detail);
    }

    private void OnViewportRelay(ViewportPrefRelayEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } player)
            return;

        if (_sandbox.IsSandboxEnabled || HasComp<GhostComponent>(player) || _staff.IsAdmin(args.SenderSession))
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

        Notify(args.SenderSession, detail);
    }

    private void Notify(ICommonSession player, string detail)
    {
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
