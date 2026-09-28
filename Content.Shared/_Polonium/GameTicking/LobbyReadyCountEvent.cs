using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.GameTicking;

/// <summary>
/// How many players in the lobby are ready, out of everyone connected who is still in the lobby.
/// </summary>
[Serializable, NetSerializable]
public sealed class LobbyReadyCountEvent : EntityEventArgs
{
    public int Ready { get; }
    public int Total { get; }

    public LobbyReadyCountEvent(int ready, int total)
    {
        Ready = ready;
        Total = total;
    }
}
