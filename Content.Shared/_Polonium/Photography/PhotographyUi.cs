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
    public readonly NetEntity Pen;
    public readonly Color Color;
    public readonly string Font;

    public PhotoSignStartMessage(NetEntity pen, Color color, string font)
    {
        Pen = pen;
        Color = color;
        Font = font;
    }
}

/// <summary>
/// Client's caption for the photo. Server re-validates the pen (still held, still a
/// <c>SignatureWriter</c>), the reach and the text; ink and font are recomputed server-side.
/// </summary>
[Serializable, NetSerializable]
public sealed class PhotoSignMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity Pen;
    public readonly string Text;

    public PhotoSignMessage(NetEntity pen, string text)
    {
        Pen = pen;
        Text = text;
    }
}
