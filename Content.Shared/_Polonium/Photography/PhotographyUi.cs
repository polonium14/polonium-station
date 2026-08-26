using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Photography;

[Serializable, NetSerializable]
public enum PhotoViewerUiKey : byte
{
    Key
}

/// <summary>
/// State pushed to photograph viewers, carrying the packed blob directly. Riding the BUI state,
/// it reaches only sessions with THIS photograph's UI open (and later openers), not every PVS
/// observer as an auto-networked component field would. <see cref="Data"/> is null when the
/// photo has no image yet.
/// </summary>
[Serializable, NetSerializable]
public sealed class PhotoViewerBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly byte[]? Data;

    public PhotoViewerBoundUserInterfaceState(byte[]? data)
    {
        Data = data;
    }
}
