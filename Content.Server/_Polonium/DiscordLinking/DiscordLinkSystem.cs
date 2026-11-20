using Content.Shared._Polonium.DiscordLinking;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Player;

namespace Content.Server._Polonium.DiscordLinking;

/// <summary>
/// Passes requests about Discord links between the clients and <see cref="DiscordLinkManager"/>.
/// </summary>
public sealed partial class DiscordLinkSystem : EntitySystem
{
    [Dependency] private DiscordLinkManager _links = default!;
    [Dependency] private IPlayerManager _player = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<DiscordLinkRequestEvent>(OnRequest);
        _player.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _player.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private async void OnRequest(DiscordLinkRequestEvent ev, EntitySessionEventArgs args)
    {
        // The account comes from the session, the client never says which account it wants to link.
        var session = args.SenderSession;
        try
        {
            var state = await _links.HandleRequest(session.UserId, session.Name, session.Channel.AuthType, ev.Start);
            Send(session, state);
        }
        catch (Exception e)
        {
            Log.Error($"Error while handling a Discord link request of {session.Name}: {e}");
        }
    }

    private async void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        var session = args.Session;
        switch (args.NewStatus)
        {
            // Like the lobby status of the game ticker, sent once the player is in game and can receive network events.
            case SessionStatus.InGame:
                try
                {
                    if (await _links.GetJoinState(session.UserId, session.Channel.AuthType) is { } state)
                        Send(session, state);
                }
                catch (Exception e)
                {
                    Log.Error($"Error while getting the Discord link of {session.Name}: {e}");
                }

                break;
            case SessionStatus.Disconnected:
                _links.ForgetPlayer(session.UserId);
                break;
        }
    }

    private void Send(ICommonSession session, DiscordLinkStateEvent state)
    {
        // The player may have left while the bot was answering.
        if (session.Status is SessionStatus.Connected or SessionStatus.InGame)
            RaiseNetworkEvent(state, session);
    }
}
