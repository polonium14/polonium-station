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

    /// <summary>Pen-written caption, null if the photo is unsigned.</summary>
    public readonly string? Caption;
    public readonly Color CaptionColor;
    public readonly string? CaptionFont;

    public PhotoViewerBoundUserInterfaceState(byte[]? data, string? caption = null, Color? captionColor = null, string? captionFont = null)
    {
        Data = data;
        Caption = caption;
        CaptionColor = captionColor ?? Color.Black;
        CaptionFont = captionFont;
    }
}

/// <summary>
/// Server tells the one user who used a pen on the photo to show the caption input, already
/// styled with that pen's ink and font so the preview matches what will be committed.
/// </summary>
[Serializable, NetSerializable]
public sealed class PhotoSignStartMessage : BoundUserInterfaceMessage
{
    public readonly Color Color;
    public readonly string Font;

    public PhotoSignStartMessage(Color color, string font)
    {
        Color = color;
        Font = font;
    }
}

/// <summary>
/// Sent by the viewer as soon as it opens: "was I opened to sign?". The server answers with
/// <see cref="PhotoSignStartMessage"/> if this user has a pending pen on this photo. Asking
/// from the client avoids racing the start message against the window's creation.
/// </summary>
[Serializable, NetSerializable]
public sealed class PhotoSignQueryMessage : BoundUserInterfaceMessage
{
}

/// <summary>
/// Client's caption for the photo. The pen is the one the server recorded when it was used on
/// the photo; server re-validates it (still held, still a <c>SignatureWriter</c>), the reach
/// and the text, and recomputes ink and font itself.
/// </summary>
[Serializable, NetSerializable]
public sealed class PhotoSignMessage : BoundUserInterfaceMessage
{
    public readonly string Text;

    public PhotoSignMessage(string text)
    {
        Text = text;
    }
}
