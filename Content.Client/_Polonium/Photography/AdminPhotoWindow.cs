using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Shared._Polonium.Photography;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client._Polonium.Photography;

/// <summary>Admin window listing every photo captured this round.</summary>
public sealed partial class AdminPhotoWindow : DefaultWindow
{
    [Dependency] private IClyde _clyde = default!;

    private new const int Size = PhotographyConstants.PhotoSizePixels;
    private const int Scale = 2;

    private readonly ItemList _list;
    private readonly TextureRect _image;
    private readonly Label _info;
    private readonly Button _delete;

    private readonly List<int> _ids = new();
    private int? _shownId;
    private OwnedTexture? _texture;

    public event Action<int>? OnSelect;
    public event Action<int>? OnDelete;

    public AdminPhotoWindow()
    {
        IoCManager.InjectDependencies(this);

        Title = Loc.GetString("admin-photo-title");
        MinSize = new Vector2(560, 400);

        _list = new ItemList
        {
            SelectMode = ItemList.ItemListSelectMode.Single,
            HorizontalExpand = true,
            VerticalExpand = true,
            MinWidth = 260,
        };
        _list.OnItemSelected += args =>
        {
            if (args.ItemIndex < _ids.Count)
                OnSelect?.Invoke(_ids[args.ItemIndex]);
        };

        _image = new TextureRect
        {
            HorizontalAlignment = HAlignment.Center,
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            TextureScale = new Vector2(Scale, Scale),
            MinSize = new Vector2(Size * Scale, Size * Scale),
        };

        _info = new Label
        {
            HorizontalAlignment = HAlignment.Center,
            Margin = new Thickness(0, 8),
        };

        _delete = new Button
        {
            Text = Loc.GetString("admin-photo-delete"),
            Disabled = true,
        };
        _delete.OnPressed += _ =>
        {
            if (_shownId is { } id)
                OnDelete?.Invoke(id);
        };

        var right = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Children = { _image, _info, _delete },
        };

        Contents.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Children = { _list, right },
        });
    }

    public void Populate(AdminPhotoEuiState state)
    {
        _list.Clear();
        _ids.Clear();
        foreach (var p in state.Photos)
        {
            var subject = string.IsNullOrEmpty(p.Subject) ? Loc.GetString("admin-photo-no-subject") : p.Subject;
            _list.AddItem(Loc.GetString("admin-photo-list-item", ("id", p.Id), ("shooter", p.Shooter), ("subject", subject)));
            _ids.Add(p.Id);
        }

        _shownId = state.SelectedId;
        _delete.Disabled = _shownId == null;
        ShowImage(state.SelectedData);
    }

    private void ShowImage(byte[]? data)
    {
        _texture?.Dispose();
        _texture = null;

        if (data == null || data.Length != PhotographyConstants.PhotoByteLength)
        {
            _image.Texture = null;
            _info.Text = Loc.GetString(_shownId == null ? "admin-photo-select" : "admin-photo-unavailable");
            return;
        }

        var owned = _clyde.CreateBlankTexture<Rgba32>(new Vector2i(Size, Size));
        owned.SetSubImage(Vector2i.Zero, new Vector2i(Size, Size), PhotoCodec.ToPixels(data));
        _texture = owned;
        _image.Texture = owned;
        _info.Text = Loc.GetString("admin-photo-number", ("id", _shownId ?? 0));
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
