using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client._Polonium.Kitchen.UI;

/// <summary>
/// Where the visible pixels of an entity's picture are and what colours they have.
/// Positions are in pixels of the sprite, counted from its centre. Until the picture has been read it describes a small box.
/// </summary>
public sealed class GrinderSpriteInfo
{
    public int Left = -8;
    public int Top = -8;
    public int Right = 7;
    public int Bottom = 7;
    public Color[] Palette = Array.Empty<Color>();

    public int CenterX => GrinderArt.Round((Left + Right + 1) / 2f);
    public int CenterY => GrinderArt.Round((Top + Bottom + 1) / 2f);

    /// <summary>
    /// Width of the visible part in art pixels.
    /// </summary>
    public int Width => (Right - Left + 1) * GrinderArt.SpriteScale;

    /// <summary>
    /// Height of the visible part in art pixels.
    /// </summary>
    public int Height => (Bottom - Top + 1) * GrinderArt.SpriteScale;
}

/// <summary>
/// Measures the pictures of entities by drawing each one once and reading the pixels back from the graphics card.
/// </summary>
public sealed class GrinderSpriteProbe
{
    private const int Size = 64;
    private const int Capacity = 96;
    private const byte AlphaThreshold = 128;

    private readonly IEntityManager _entMan;
    private readonly IClyde _clyde;
    private readonly Dictionary<(EntityUid Uid, Direction Facing), GrinderSpriteInfo> _known = new();
    private readonly List<(EntityUid Uid, Direction Facing, GrinderSpriteInfo Info)> _pending = new();
    private IRenderTexture? _target;

    public GrinderSpriteProbe(IEntityManager entMan, IClyde clyde)
    {
        _entMan = entMan;
        _clyde = clyde;
    }

    /// <summary>
    /// What is known about the entity's picture from the given side. The same object is filled in a few frames after the first request.
    /// </summary>
    public GrinderSpriteInfo Get(EntityUid uid, Direction facing = Direction.South)
    {
        if (_known.TryGetValue((uid, facing), out var info))
            return info;

        if (_known.Count >= Capacity)
            _known.Clear();

        info = new GrinderSpriteInfo();
        _known[(uid, facing)] = info;
        _pending.Add((uid, facing, info));
        return info;
    }

    /// <summary>
    /// Draws and reads the pictures that were asked for since the last call.
    /// </summary>
    public void Render(DrawingHandleScreen handle)
    {
        if (_pending.Count == 0)
            return;

        _target ??= _clyde.CreateRenderTarget(new Vector2i(Size, Size), new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb), name: "grinder-probe");
        var target = _target;
        foreach (var (uid, facing, info) in _pending)
        {
            if (_entMan.Deleted(uid) || !_entMan.TryGetComponent(uid, out SpriteComponent? sprite))
                continue;

            handle.RenderInRenderTarget(target,
                () =>
                {
                    handle.SetTransform(Matrix3x2.Identity);
                    handle.DrawEntity(uid, new Vector2(Size / 2f), Vector2.One, Angle.Zero, overrideDirection: facing, sprite: sprite);
                },
                Color.Transparent);
            target.CopyPixelsToMemory<Rgba32>(image =>
            {
                using (image)
                {
                    Read(info, image);
                }
            });
        }

        _pending.Clear();
    }

    private static void Read(GrinderSpriteInfo info, Image<Rgba32> image)
    {
        var span = image.GetPixelSpan();
        var stride = image.Width;
        int left = image.Width, top = image.Height, right = -1, bottom = -1;
        var palette = new List<Color>();
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var pixel = span[y * stride + x];
                if (pixel.A < AlphaThreshold)
                    continue;

                left = Math.Min(left, x);
                right = Math.Max(right, x);
                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y);
                if ((x + y * 3) % 4 == 0)
                    palette.Add(new Color(pixel.R, pixel.G, pixel.B));
            }
        }

        if (right < 0)
            return;

        info.Left = left - image.Width / 2;
        info.Right = right - image.Width / 2;
        info.Top = top - image.Height / 2;
        info.Bottom = bottom - image.Height / 2;
        info.Palette = palette.ToArray();
    }

    /// <summary>
    /// Lets go of the graphics memory. Pictures are measured again when somebody asks for them.
    /// </summary>
    public void Release()
    {
        _target?.Dispose();
        _target = null;
        _known.Clear();
        _pending.Clear();
    }
}
