// SPDX-FileCopyrightText: 2025 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2025 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Threading.Tasks;
using Content.Server.GameTicking;
using Content.Shared.CCVar;
using Robust.Server.Player;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Discord.Managers;
public sealed class DiscordBanNotifyManager
{
    [Dependency] private readonly DiscordWebhook _dc = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly ILogManager _log = default!;
    [Dependency] private readonly ILocalizationManager _loc = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntitySystemManager _systems = default!;

    private ISawmill _logger = default!;

    public void Initialize()
    {
        _logger = _log.GetSawmill("discord-ban-notify");
    }

    public async Task SendBanNotification(
        string adminName,
        string targetName,
        string reason,
        long expirationTime,
        long issuanceTime,
        int? issuanceRnd,
        int? situationRnd,
        List<string>? roleNames)
    {
        var isRoleBan = roleNames is { Count: > 0 };

        List<WebhookEmbedField> fields =
        [
            new()
            {
                Inline = true,
                Name = _loc.GetString("ban-notify-field-banned-user-title"),
                Value = targetName,
            },
            new()
            {
                Inline = false,
                Name = _loc.GetString("ban-notify-field-ban-reason-title"),
                Value = situationRnd is null or 0
            ? (issuanceRnd != null
                ? $"**#{issuanceRnd}** | {reason}"
                : reason)
            : $"**#{situationRnd}** | {reason}",
            },
            new()
            {
                Inline = false,
                Name = _loc.GetString("ban-notify-field-ban-issued-by-title"),
                Value = adminName
            },
            new()
            {
                Inline = true,
                Name = _loc.GetString("ban-notify-field-ban-issued-on-title"),
                Value = issuanceRnd == null ? $"<t:{issuanceTime}:d>" : $"<t:{issuanceTime}:d>, #{issuanceRnd}",
            },
            new()
            {
                Inline = true,
                Name = _loc.GetString("ban-notify-field-ban-expires-title"),
                Value = expirationTime <= 0 ? _loc.GetString("ban-notify-ban-expires-never") : $"<t:{expirationTime}:R>",
            },
        ];

        if (isRoleBan)
        {
            fields.Add(new WebhookEmbedField
            {
                Inline = false,
                Name = _loc.GetString("ban-notify-banned-roles-title"),
                Value = $"```{string.Join("", roleNames!.Select(r => $"- {r}\n"))}```",
            });
        }

        await SendBanNotification(new WebhookEmbed
        {
            Title = isRoleBan ? _loc.GetString("ban-notify-role-ban-embed-title") : _loc.GetString("ban-notify-ban-embed-title"),
            Fields = fields,
            Footer = new()
            {
                Text = _loc.GetString("ban-notify-embed-footer", ("serverName", _cfg.GetCVar(CVars.GameHostName))),
            },
            Color = Color.Red.ToArgb() & 16777215,
        });
    }

    public async Task SendBanNotification(
        WebhookEmbed embed
    )
    {
        var webhookUri = _cfg.GetCVar(CCVars.BanWebhook);

        if (webhookUri == string.Empty)
            return;

        WebhookIdentifier? webhook = null;

        await _dc.GetWebhook(webhookUri, w => webhook = w.ToIdentifier());

        if (webhook == null)
            return;

        try
        {
            var payload = new WebhookPayload { Embeds = [embed] };
            await _dc.CreateMessage(webhook.Value, payload);
        }
        catch (Exception)
        {
            _logger.Error(_loc.GetString("ban-notify-webhook-error-message"));
        }
    }

    public void SendRoleBanNotification(
        NetUserId? target,
        string? targetUsername,
        NetUserId? banningAdmin,
        uint? minutes,
        string reason,
        int? situationRound,
        List<string> roleNames)
    {
        DateTimeOffset? expires = null;
        if (minutes > 0)
        {
            expires = DateTimeOffset.Now + TimeSpan.FromMinutes(minutes.Value);
        }

        _systems.TryGetEntitySystem(out GameTicker? ticker);
        int? roundId = ticker == null || ticker.RoundId == 0 ? null : ticker.RoundId;

        ICommonSession? session = null;
        if (target != null)
            _playerManager.TryGetSessionById(target.Value, out session);

        var isAdminFetched = _playerManager.TryGetSessionById(banningAdmin, out var adminSession);

        _ = SendBanNotification(
            isAdminFetched ? adminSession!.Name : _loc.GetString("ban-notify-ban-admin-unknown"),
            targetUsername ?? (session != null ? session.Name : _loc.GetString("ban-notify-ban-target-user-unknown")),
            reason,
            expires.GetValueOrDefault().ToUnixTimeSeconds(),
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            roundId,
            situationRound,
            roleNames
        );
    }
}

