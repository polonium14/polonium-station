// SPDX-FileCopyrightText: 2025 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
// SPDX-FileCopyrightText: 2025 Simon <63975668+Simyon264@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 sleepyyapril <123355664+sleepyyapril@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 taydeo <tay@funkystation.org>
// SPDX-FileCopyrightText: 2026 taydeo <td12233a@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Threading.Tasks;
using Content.Server.Chat.Managers;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using NetCord.Gateway;
using Robust.Shared.Asynchronous;
using Robust.Shared.Configuration;

namespace Content.Server.Discord.DiscordLink;

public sealed class DiscordChatLink : IPostInjectInit
{
    [Dependency] private readonly DiscordLink _discordLink = default!;
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly ITaskManager _taskManager = default!;
    [Dependency] private readonly ILogManager _logManager = default!;

    private ISawmill _sawmill = default!;

    private ulong? _oocChannelId;
    private ulong? _adminChannelId;

    public void Initialize()
    {
        _discordLink.OnMessageReceived += OnMessageReceived;

        _configurationManager.OnValueChanged(CCVars.OocDiscordChannelId, OnOocChannelIdChanged, true);
        _configurationManager.OnValueChanged(CCVars.AdminChatDiscordChannelId, OnAdminChannelIdChanged, true);
    }

    public void Shutdown()
    {
        _discordLink.OnMessageReceived -= OnMessageReceived;

        _configurationManager.UnsubValueChanged(CCVars.OocDiscordChannelId, OnOocChannelIdChanged);
        _configurationManager.UnsubValueChanged(CCVars.AdminChatDiscordChannelId, OnAdminChannelIdChanged);
    }

    private void OnOocChannelIdChanged(string channelId)
    {
        _oocChannelId = TryParseChannelId(channelId, CCVars.OocDiscordChannelId.Name);
    }

    private void OnAdminChannelIdChanged(string channelId)
    {
        _adminChannelId = TryParseChannelId(channelId, CCVars.AdminChatDiscordChannelId.Name);
    }

    private ulong? TryParseChannelId(string channelId, string cvarName)
    {
        if (string.IsNullOrEmpty(channelId))
            return null;

        if (ulong.TryParse(channelId, out var id))
            return id;

        _sawmill.Error($"Invalid Discord channel ID in {cvarName}: '{channelId}'");
        return null;
    }

    private void OnMessageReceived(Message message)
    {
        if (message.Author.IsBot)
            return;

        var contents = message.Content.ReplaceLineEndings(" ");

        if (message.ChannelId == _oocChannelId)
        {
            _taskManager.RunOnMainThread(() => _chatManager.SendHookOOC(message.Author.Username, contents));
        }
        else if (message.ChannelId == _adminChannelId)
        {
            _taskManager.RunOnMainThread(() => _chatManager.SendHookAdmin(message.Author.Username, contents));
        }
    }

    public async void SendMessage(string message, string author, ChatChannel channel)
    {
        var channelId = channel switch
        {
            ChatChannel.OOC => _oocChannelId,
            ChatChannel.AdminChat => _adminChannelId,
            _ => throw new InvalidOperationException("Channel not linked to Discord."),
        };

        if (channelId == null)
        {
            // Configuration not set up. Ignore.
            return;
        }

        // @ and < are both problematic for discord due to pinging. / is sanitized solely to kneecap links to murder embeds via blunt force
        message = message.Replace("@", "\\@").Replace("<", "\\<").Replace("/", "\\/");

        try
        {
            await _discordLink.SendMessageAsync(channelId.Value, $"**{channel.GetString()}**: `{author}`: {message}");
        }
        catch (Exception e)
        {
            _sawmill.Error($"Error while sending Discord message: {e}");
        }
    }

    void IPostInjectInit.PostInject()
    {
        _sawmill = _logManager.GetSawmill("discord.chat");
    }
}
