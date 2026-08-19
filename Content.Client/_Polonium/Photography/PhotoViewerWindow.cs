using System.Numerics;
using Content.Shared._Polonium.Photography;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client._Polonium.Photography;

/// <summary>Shows a developed photograph as a bare polaroid - no window chrome, just the card: pixel image in a dark inset on off-white paper, fat bottom caption strip, top-right X, drag-to-move. Texture is built from the unpacked RGB565 blob (no image decoder runs).</summary>
public sealed partial class PhotoViewerWindow : BaseWindow
{
    [Dependency] private IClyde _clyde = default!;

    private new const int Size = PhotographyConstants.PhotoSizePixels;
    private const int Scale = 3;

    private static readonly Color PaperColor = Color.FromHex("#F3EFE4");
    private static readonly Color InsetColor = Color.FromHex("#1A1A1A");

    private readonly TextureRect _image;
    private readonly PanelContainer _photoInset;
    private readonly Label _caption;
    private readonly Label _empty;
    private OwnedTexture? _texture;

    public PhotoViewerWindow()
    {
        IoCManager.InjectDependencies(this);

        MouseFilter = MouseFilterMode.Stop;
        Resizable = false;

        _image = new TextureRect
        {
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            TextureScale = new Vector2(Scale, Scale),
            MinSize = new Vector2(Size * Scale, Size * Scale),
        };

        _photoInset = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = InsetColor,
                ContentMarginLeftOverride = 2,
                ContentMarginRightOverride = 2,
                ContentMarginTopOverride = 2,
                ContentMarginBottomOverride = 2,
            },
            Children = { _image },
        };

        _empty = new Label
        {
            Text = Loc.GetString("photo-viewer-empty"),
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            MinSize = new Vector2(Size * Scale, Size * Scale),
            Visible = false,
        };

        _caption = new Label
        {
            Text = string.Empty,
            HorizontalAlignment = HAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0),
        };

        var column = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Children = { _photoInset, _empty, _caption },
        };

        var card = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = PaperColor,
                ContentMarginLeftOverride = 16,
                ContentMarginRightOverride = 16,
                ContentMarginTopOverride = 16,
                ContentMarginBottomOverride = 44,
            },
            Children = { column },
        };

        var close = new Button
        {
            Text = "✕",
            MinSize = new Vector2(24, 24),
        };
        close.OnPressed += _ => Close();

        var header = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Children = { new Control { HorizontalExpand = true }, close },
        };

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Children = { header, card },
        };

        AddChild(root);
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
    {
        return DragMode.Move;
    }

    public void Populate(byte[]? data)
    {
        if (data == null || data.Length != PhotographyConstants.PhotoByteLength)
        {
            _photoInset.Visible = false;
            _empty.Visible = true;
            return;
        }

        _texture?.Dispose();

        var owned = _clyde.CreateBlankTexture<Rgba32>(new Vector2i(Size, Size));
        owned.SetSubImage(Vector2i.Zero, new Vector2i(Size, Size), PhotoCodec.ToPixels(data));

        _texture = owned;
        _image.Texture = owned;
        _photoInset.Visible = true;
        _empty.Visible = false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _texture?.Dispose();
            _texture = null;
        }

        base.Dispose(disposing);
    }
}
