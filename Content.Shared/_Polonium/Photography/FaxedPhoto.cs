namespace Content.Shared._Polonium.Photography;

/// <summary>
/// A photograph on its way through a fax. Holds the picture itself, so a queued printout
/// survives the original being destroyed.
/// </summary>
public sealed record FaxedPhoto(
    byte[]? Data,
    string? Subject,
    string? Shooter,
    string? Caption,
    Color CaptionColor,
    string? CaptionFont);

/// <summary>
/// Raised on an item a fax is about to scan, so a photograph can add its picture to the printout.
/// </summary>
[ByRefEvent]
public record struct FaxScanPhotoEvent
{
    public FaxedPhoto? Photo;
}

/// <summary>
/// Raised on the entity a fax printed from a printout that carries a picture.
/// </summary>
[ByRefEvent]
public readonly record struct FaxPhotoPrintedEvent(FaxedPhoto Photo);
