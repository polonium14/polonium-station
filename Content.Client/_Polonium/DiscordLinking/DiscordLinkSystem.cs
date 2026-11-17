using Content.Shared._Polonium.DiscordLinking;

namespace Content.Client._Polonium.DiscordLinking;

/// <summary>
/// Keeps what the server last said about the Discord link of the local player.
/// </summary>
public sealed class DiscordLinkSystem : EntitySystem
{
    /// <summary>
    /// The last answer of the server, null until it has said anything.
    /// </summary>
    public DiscordLinkStateEvent? State { get; private set; }

    public event Action<DiscordLinkStateEvent>? StateChanged;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<DiscordLinkStateEvent>(OnState);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        State = null;
    }

    private void OnState(DiscordLinkStateEvent ev)
    {
        State = ev;
        StateChanged?.Invoke(ev);
    }

    /// <summary>
    /// Asks the server about the link, the answer comes with <see cref="StateChanged"/>.
    /// </summary>
    public void RequestStatus()
    {
        RaiseNetworkEvent(new DiscordLinkRequestEvent(false));
    }

    /// <summary>
    /// Asks the server to start linking, the page to open in the browser comes with <see cref="StateChanged"/>.
    /// </summary>
    public void RequestStart()
    {
        RaiseNetworkEvent(new DiscordLinkRequestEvent(true));
    }
}
