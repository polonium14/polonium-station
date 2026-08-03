using Robust.Shared.Serialization;

namespace Content.Shared.Paper;

/// <summary>
///     Sent from the server to a single client to tell it to open the stamp
///     placement UI for the given paper, letting the player position and rotate
///     the stamp before committing it. Mirrors the signature placement flow, but
///     stamps place at their natural size (no scale).
/// </summary>
[Serializable, NetSerializable]
public sealed class PaperStampRequestEvent : EntityEventArgs
{
    public readonly NetEntity Paper;
    public readonly NetEntity Stamp;

    public PaperStampRequestEvent(NetEntity paper, NetEntity stamp)
    {
        Paper = paper;
        Stamp = stamp;
    }
}
