using Robust.Shared.GameStates;

namespace Content.Shared._Polonium.Photography;

/// <summary>
/// A developed photograph. Carries ONLY the storage id, never the pixel bytes: the blob lives
/// in the server-side per-round store and is streamed to a single viewer via BUI state on open.
/// A <c>byte[]</c> on an <c>AutoNetworkedField</c> would replicate to every PVS client and
/// re-send for each new observer.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PoloniumPhotographComponent : Component
{
    /// <summary>Key into the server's <c>PhotoStorageManager</c>. 0 means "no image yet" (e.g. the client never answered a capture request).</summary>
    [DataField, AutoNetworkedField]
    public int PhotoId;
}
