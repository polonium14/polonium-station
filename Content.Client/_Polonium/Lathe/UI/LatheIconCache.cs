using System.Numerics;
using Content.Shared.Research.Prototypes;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Utility;
using Robust.Shared.Map;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client._Polonium.Lathe.UI;

public sealed class LatheIcon
{
    public IRenderTexture? Target;
    public Vector2i Size;

    /// <summary>
    /// The part of the picture that is not empty.
    /// </summary>
    public UIBox2i Bounds;

    /// <summary>
    /// Every visible pixel in the order a plotter head would draw it: row by row, turning around at each end.
    /// Null until the picture has been read back from the graphics card.
    /// </summary>
    public LatheIconPixel[]? Pixels;

    public LatheRecipePrototype? Pending;
    public EntityUid Dummy;
    public Texture? Source;
    public int Age;
    public int LastUsed;
    public bool Rendered;
    public bool PixelsRequested;

    public Texture? Texture => Rendered ? Target?.Texture : null;
}

public readonly record struct LatheIconPixel(byte X, byte Y, Color Color);

/// <summary>
/// Keeps one small picture per recipe, drawn from the real entity so that layered sprites look as they do in the world.
/// </summary>
public sealed class LatheIconCache
{
    private const int Capacity = 160;
    private const int KeepFrames = 600;
    private const int MaxSize = 64;
    private const byte AlphaThreshold = 40;

    private readonly IEntityManager _entMan;
    private readonly IClyde _clyde;
    private readonly SpriteSystem _sprite;
    private readonly Dictionary<string, LatheIcon> _icons = new();
    private readonly List<string> _stale = new();
    private int _frame;

    public LatheIconCache(IEntityManager entMan, IClyde clyde)
    {
        _entMan = entMan;
        _clyde = clyde;
        _sprite = entMan.System<SpriteSystem>();
    }

    public LatheIcon Get(LatheRecipePrototype recipe, bool pixels = false)
    {
        if (!_icons.TryGetValue(recipe.ID, out var icon))
        {
            icon = new LatheIcon { Pending = recipe };
            _icons[recipe.ID] = icon;
        }

        icon.LastUsed = _frame;
        icon.PixelsRequested |= pixels;
        return icon;
    }

    private void Prepare(LatheIcon icon, LatheRecipePrototype recipe)
    {
        if (recipe.Icon != null)
        {
            icon.Source = _sprite.Frame0(recipe.Icon);
            icon.Size = icon.Source.Size;
        }
        else if (recipe.Result is { } result)
        {
            icon.Dummy = _entMan.Spawn(result, MapCoordinates.Nullspace);
            if (_entMan.TryGetComponent(icon.Dummy, out SpriteComponent? sprite))
            {
                foreach (var layer in sprite.AllLayers)
                {
                    if (layer.Visible)
                        icon.Size = Vector2i.ComponentMax(icon.Size, layer.PixelSize);
                }
            }
        }

        icon.Size = Vector2i.ComponentMin(icon.Size, new Vector2i(MaxSize, MaxSize));
        if (icon.Size.X > 0 && icon.Size.Y > 0)
        {
            icon.Target = _clyde.CreateRenderTarget(icon.Size, new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb), name: "lathe-icon");
            icon.Bounds = UIBox2i.FromDimensions(Vector2i.Zero, icon.Size);
        }
    }

    public void FrameUpdate()
    {
        _frame++;
        foreach (var (id, icon) in _icons)
        {
            if (icon.Pending is { } recipe)
            {
                icon.Pending = null;
                Prepare(icon, recipe);
            }

            icon.Age++;
            if (icon.Rendered && icon.Dummy.IsValid())
            {
                _entMan.DeleteEntity(icon.Dummy);
                icon.Dummy = EntityUid.Invalid;
            }

            if (_icons.Count > Capacity && _frame - icon.LastUsed > KeepFrames)
                _stale.Add(id);
        }

        foreach (var id in _stale)
        {
            Release(_icons[id]);
            _icons.Remove(id);
        }

        _stale.Clear();
    }

    /// <summary>
    /// Draws the pictures that are waiting for it. A new entity gets one frame to settle its appearance first.
    /// </summary>
    public void Render(DrawingHandleScreen handle)
    {
        foreach (var icon in _icons.Values)
        {
            if (icon.Target is not { } target)
                continue;

            if (!icon.Rendered && icon.Age >= 2)
            {
                var center = (Vector2) icon.Size / 2f;
                handle.RenderInRenderTarget(target,
                    () =>
                    {
                        // The window's place on the screen must not shift the picture inside its own target.
                        handle.SetTransform(Matrix3x2.Identity);
                        if (icon.Source != null)
                            handle.DrawTextureRect(icon.Source, UIBox2.FromDimensions(Vector2.Zero, icon.Size));
                        else if (_entMan.EntityExists(icon.Dummy))
                            handle.DrawEntity(icon.Dummy, center, Vector2.One, Angle.Zero, overrideDirection: Direction.South);
                    },
                    Color.Transparent);
                icon.Rendered = true;
                icon.Source = null;
            }

            if (icon.Rendered && icon.PixelsRequested && icon.Pixels == null)
            {
                icon.Pixels = Array.Empty<LatheIconPixel>();
                target.CopyPixelsToMemory<Rgba32>(image =>
                {
                    using (image)
                    {
                        Read(icon, image);
                    }
                });
            }
        }
    }

    private static void Read(LatheIcon icon, Image<Rgba32> image)
    {
        var span = image.GetPixelSpan();
        var stride = image.Width;
        var width = Math.Min(image.Width, byte.MaxValue);
        var height = Math.Min(image.Height, byte.MaxValue);
        int left = width, top = height, right = -1, bottom = -1;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (span[y * stride + x].A <= AlphaThreshold)
                    continue;

                left = Math.Min(left, x);
                right = Math.Max(right, x);
                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y);
            }
        }

        if (right < 0)
            return;

        var pixels = new List<LatheIconPixel>();
        for (var y = top; y <= bottom; y++)
        {
            var start = pixels.Count;
            for (var x = left; x <= right; x++)
            {
                var pixel = span[y * stride + x];
                if (pixel.A > AlphaThreshold)
                    pixels.Add(new LatheIconPixel((byte) x, (byte) y, new Color(pixel.R, pixel.G, pixel.B)));
            }

            if ((y - top) % 2 == 1)
                pixels.Reverse(start, pixels.Count - start);
        }

        icon.Bounds = new UIBox2i(left, top, right + 1, bottom + 1);
        icon.Pixels = pixels.ToArray();
    }

    /// <summary>
    /// Lets go of every picture. They are drawn again when somebody asks for them.
    /// </summary>
    public void Clear()
    {
        foreach (var icon in _icons.Values)
        {
            Release(icon);
        }

        _icons.Clear();
    }

    private void Release(LatheIcon icon)
    {
        if (icon.Dummy.IsValid())
            _entMan.DeleteEntity(icon.Dummy);

        icon.Dummy = EntityUid.Invalid;
        icon.Target?.Dispose();
        icon.Target = null;
    }
}
