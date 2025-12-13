// SPDX-FileCopyrightText: 2025 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2025 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared.CCVar;
using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Server.Discord.Managers;
public sealed class DiscordBanNotifyManager
{
    [Dependency] private readonly DiscordWebhook _dc = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly ILogManager _log = default!;
    [Dependency] private readonly ILocalizationManager _loc = default!;

    private ISawmill _logger = default!;

    public void Initialize()
    {
        _logger = _log.GetSawmill("discord-ban-notify");
    }

    public async Task DiscordBanNotify(
        string adminName,
        string targetName,
        string reason,
        long expirationTime,
        long issuanceTime,
        int? issuanceRnd,
        int? situationRnd,
        List<ServerRoleBanDef>? roleBans)
    {
        var isRoleBan = roleBans != null;

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

        if (roleBans != null)
        {
            fields.Add(new WebhookEmbedField
            {
                Inline = false,
                Name = _loc.GetString("ban-notify-banned-roles-title"),
                Value = $"```{string.Join("", roleBans.Select(b => b.Role.Contains(':') ? $"- {b.Role.Split(':')[1]}\n" : $"- {b.Role}\n"))}```",
            });
        }

        await DiscordBanNotify(new WebhookEmbed
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

    public async Task DiscordBanNotify(
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
}

