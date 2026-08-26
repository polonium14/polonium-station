using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Polonium.Photography;

/// <summary>
/// Server tells the single shutter-pressing client to render around <see cref="Coordinates"/>
/// and send pixels back. <see cref="CaptureId"/> is a one-shot token held pending; a submission
/// not matching a pending token for that session is rejected, so a client can't upload
/// photos it was never authorized to take.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestPhotoCaptureEvent : EntityEventArgs
{
    public readonly int CaptureId;

    /// <summary>Center of the region to photograph (usually the camera's tile).</summary>
    public readonly NetCoordinates Coordinates;

    /// <summary>If on, the client lights its capture render.</summary>
    public readonly bool Flash;

    public RequestPhotoCaptureEvent(int captureId, NetCoordinates coordinates, bool flash)
    {
        CaptureId = captureId;
        Coordinates = coordinates;
        Flash = flash;
    }
}

/// <summary>
/// Client's answer to a <see cref="RequestPhotoCaptureEvent"/>: the given token plus an RGB565
/// image of exactly <see cref="PhotographyConstants.PhotoByteLength"/> bytes. Server validates
/// length and token before storing anything.
/// </summary>
[Serializable, NetSerializable]
public sealed class SubmitPhotoEvent : EntityEventArgs
{
    public readonly int CaptureId;
    public readonly byte[] Data;

    public SubmitPhotoEvent(int captureId, byte[] data)
    {
        CaptureId = captureId;
        Data = data;
    }
}
