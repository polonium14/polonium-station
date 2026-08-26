namespace Content.Shared._Polonium.Photography;

/// <summary>
/// Wraps a captured-photo id in an admin log's structured <c>Values</c> so the photography
/// half of <c>AdminLogManager</c> (see AdminLogManager.PhotoLinks.cs) can build a "jump to
/// this photo" chat link - the same way <c>SerializablePlayer</c> drives tpto links. Extracted
/// by type, not key. <see cref="ToString"/> returns the bare number so the log text reads naturally.
/// </summary>
public readonly record struct LoggablePhotoId(int Id)
{
    public override string ToString() => Id.ToString();
}
