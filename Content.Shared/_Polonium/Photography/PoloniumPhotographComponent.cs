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
    [AutoNetworkedField]
    public int PhotoId;

    /// <summary>Text written on the bottom strip with a pen. Null until signed; a photo can be signed only once.</summary>
    [AutoNetworkedField]
    public string? Caption;

    /// <summary>Ink color of <see cref="Caption"/>, taken from the pen (or crayon) at signing time.</summary>
    [AutoNetworkedField]
    public Color CaptionColor = Color.Black;

    /// <summary>Font prototype id of <see cref="Caption"/>, taken from the pen at signing time.</summary>
    [AutoNetworkedField]
    public string? CaptionFont;
}
