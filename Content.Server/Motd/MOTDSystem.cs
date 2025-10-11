// SPDX-FileCopyrightText: 2023 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 TemporalOroboros <TemporalOroboros@gmail.com>
// SPDX-FileCopyrightText: 2023 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 LordCarve <27449516+LordCarve@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
// SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
//
// SPDX-License-Identifier: MIT

using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Robust.Shared.Console;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Server.Motd;

/// <summary>
/// The system that handles broadcasting the Message Of The Day to players when they join the lobby/the MOTD changes/they ask for it to be printed.
/// </summary>
public sealed class MOTDSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;

    /// <summary>
    /// The cached value of the Message of the Day. Used for fast access.
    /// </summary>
    private string _messageOfTheDay = "";

    public override void Initialize()
    {
        base.Initialize();
        Subs.CVar(_configurationManager, CCVars.MOTD, OnMOTDChanged, invokeImmediately: true);
        Subs.CVar(_configurationManager, CCVars.MOTDServer, FetchMOTD, invokeImmediately: true);
        SubscribeLocalEvent<PlayerJoinedLobbyEvent>(OnPlayerJoinedLobby);
    }

    public void FetchMOTD(string url)
    {
        if (string.IsNullOrEmpty(url))
            return;

        Task.Run(async () =>
        {
            try
            {
                using HttpClient httpClient = new();
                using var statusCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var health = await httpClient.GetAsync($"{url}/status", statusCts.Token);

                if (!health.IsSuccessStatusCode)
                {
                    var err = await health.Content.ReadFromJsonAsync<MotdSrvErr>(cancellationToken: statusCts.Token);
                    Log.Error(
                        $"An error occurred while fetching MOTD from the remote server (url: {url}): {err?.message ?? err?.error ?? "No message"}");
                    return;
                }

                var status = await health.Content.ReadFromJsonAsync<MotdSrvHealth>(cancellationToken: statusCts.Token);
                if (status is not { status: "ok" })
                {
                    Log.Error(
                        $"An error occurred while fetching MOTD from the remote server (url: {url}): Invalid status response");
                    return;
                }

                using var motdCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var info = await httpClient.GetAsync($"{url}/motd", motdCts.Token);

                if (!info.IsSuccessStatusCode)
                {
                    var err = await info.Content.ReadFromJsonAsync<MotdSrvErr>(cancellationToken: motdCts.Token);
                    Log.Error(
                        $"An error occured while fetching MOTD from the remote server (url: {url}): {err?.message ?? (err?.error ?? "No message")}");
                    return;
                }

                var motdInfo = await info.Content.ReadFromJsonAsync<MotdSrvInfo>(cancellationToken: motdCts.Token);

                var newMotd = motdInfo?.exists == true ? motdInfo.content : null;

                if (newMotd == null)
                {
                    Log.Error($"An error occurred while fetching MOTD from the remote server (url: {url})");
                    return;
                }

                _configurationManager.SetCVar(CCVars.MOTD, newMotd);
                Log.Info($"Fetched MOTD from remote server (url: {url}) at {status?.timestamp}");
            }
            catch (OperationCanceledException)
            {
                Log.Error($"HTTP request timed out while fetching MOTD from the remote server (url: {url}).");
            }
            catch (Exception e)
            {
                Log.Error($"Caught an exception while fetching MOTD from the remote server (url: {url}): {e.Message}");
            }
        });
    }

    /// <summary>
    /// Sends the Message Of The Day, if any, to all connected players.
    /// </summary>
    public void TrySendMOTD()
    {
        if (string.IsNullOrEmpty(_messageOfTheDay))
            return;

        var wrappedMessage = Loc.GetString("motd-wrap-message", ("motd", _messageOfTheDay));
        _chatManager.ChatMessageToAll(ChatChannel.Server, _messageOfTheDay, wrappedMessage, source: EntityUid.Invalid, hideChat: false, recordReplay: true);
    }

    /// <summary>
    /// Sends the Message Of The Day, if any, to a specific player.
    /// </summary>
    public void TrySendMOTD(ICommonSession player)
    {
        if (string.IsNullOrEmpty(_messageOfTheDay))
            return;

        var wrappedMessage = Loc.GetString("motd-wrap-message", ("motd", _messageOfTheDay));
        _chatManager.ChatMessageToOne(ChatChannel.Server, _messageOfTheDay, wrappedMessage, source: EntityUid.Invalid, hideChat: false, client: player.Channel);
    }

    /// <summary>
    /// Sends the Message Of The Day, if any, to a specific player's console and chat.
    /// </summary>
    /// <remarks>
    /// This is used by the MOTD console command because we can't tell whether the player is using `console or /console so we send the message to both.
    /// </remarks>
    public void TrySendMOTD(IConsoleShell shell)
    {
        if (string.IsNullOrEmpty(_messageOfTheDay))
            return;

        var wrappedMessage = Loc.GetString("motd-wrap-message", ("motd", _messageOfTheDay));
        shell.WriteLine(wrappedMessage);
        if (shell.Player is { } player)
            _chatManager.ChatMessageToOne(ChatChannel.Server, _messageOfTheDay, wrappedMessage, source: EntityUid.Invalid, hideChat: false, client: player.Channel);
    }

    #region Event Handlers

    /// <summary>
    /// Posts the Message Of The Day to any players who join the lobby.
    /// </summary>
    private void OnPlayerJoinedLobby(PlayerJoinedLobbyEvent ev)
    {
        TrySendMOTD(ev.PlayerSession);
    }

    /// <summary>
    /// Broadcasts changes to the Message Of The Day to all players.
    /// </summary>
    private void OnMOTDChanged(string val)
    {
        if (val == _messageOfTheDay)
            return;

        _messageOfTheDay = val;
        TrySendMOTD();
    }

    #endregion Event Handlers


    private record MotdSrvInfo(bool exists, string id, string reactions, string author, string content);
    private record MotdSrvHealth(string status, TimeSpan timestamp);
    private record MotdSrvErr(string? message, string? error);
}

