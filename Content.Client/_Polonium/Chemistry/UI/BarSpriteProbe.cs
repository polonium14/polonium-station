using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client._Polonium.Chemistry.UI;

/// <summary>
/// Where the visible pixels of an entity's picture are, in pixels of the sprite counted from its centre.
/// </summary>
public sealed class BarSpriteInfo
{
    /// <summary>
    /// False until the picture has been read. Until then the numbers describe a small box.
    /// </summary>
    public bool Measured;

    public int Left = -8;
    public int Top = -8;
    public int Right = 7;
    public int Bottom = 7;

    /// <summary>
    /// Middle of the part the thing stands on. A mug with a handle stands on its cup, not on the middle of its picture.
    /// </summary>
    public float Axis;

    /// <summary>
    /// Right end of the part the thing stands on.
    /// </summary>
    public int FootEdge = 8;

    /// <summary>
    /// Takes over the measurements of another picture.
    /// </summary>
    public void CopyFrom(BarSpriteInfo other)
    {
        Measured = other.Measured;
        Left = other.Left;
        Top = other.Top;
        Right = other.Right;
        Bottom = other.Bottom;
        Axis = other.Axis;
        FootEdge = other.FootEdge;
    }

    public int Width => Right - Left + 1;
    public int Height => Bottom - Top + 1;
    public float Center => (Left + Right + 1) / 2f;
}

/// <summary>
/// Measures the pictures of entities by drawing each one once and reading the pixels back from the graphics card.
/// </summary>
public sealed class BarSpriteProbe
{
    private const int Size = 64;
    private const int Capacity = 128;
    private const int FootRows = 3;
    private const byte AlphaThreshold = 128;

    private readonly IEntityManager _entMan;
    private readonly IClyde _clyde;
    private readonly SpriteSystem _sprites;
    private readonly Dictionary<EntityUid, BarSpriteInfo> _known = new();
    private readonly Dictionary<int, BarSpriteInfo> _looks = new();
    private readonly List<(EntityUid Uid, BarSpriteInfo Info)> _pending = new();
    private IRenderTexture? _target;

    public BarSpriteProbe(IEntityManager entMan, IClyde clyde)
    {
        _entMan = entMan;
        _clyde = clyde;
        _sprites = entMan.System<SpriteSystem>();
    }

    /// <summary>
    /// What is known about the entity's picture. The same object is filled in a few frames after the first request.
    /// </summary>
    public BarSpriteInfo Get(EntityUid uid)
    {
        if (_known.TryGetValue(uid, out var info))
            return info;

        if (_known.Count >= Capacity)
            _known.Clear();

        info = new BarSpriteInfo();
        _known[uid] = info;
        if (_looks.TryGetValue(Look(uid), out var seen))
            info.CopyFrom(seen);
        else
            _pending.Add((uid, info));

        return info;
    }

    /// <summary>
    /// A number that changes whenever the entity's sprite shows another picture.
    /// </summary>
    public int Look(EntityUid uid)
    {
        if (!_entMan.TryGetComponent(uid, out SpriteComponent? sprite))
            return 0;

        var look = new HashCode();
        for (var i = 0; _sprites.TryGetLayer((uid, sprite), i, out var layer, false); i++)
        {
            look.Add(layer.Visible);
            look.Add(layer.State.Name);
            look.Add(layer.ActualRsi?.Path);
        }

        return look.ToHashCode();
    }

    /// <summary>
    /// Measures the picture again, for a thing that changes its shape as a glass does with the drink in it.
    /// A look that was measured before is known at once, a new one a few frames later.
    /// </summary>
    public void Remeasure(EntityUid uid)
    {
        var info = Get(uid);
        if (_looks.TryGetValue(Look(uid), out var seen))
        {
            info.CopyFrom(seen);
            return;
        }

        foreach (var (pending, _) in _pending)
        {
            if (pending == uid)
                return;
        }

        _pending.Add((uid, info));
    }

    /// <summary>
    /// Draws and reads the pictures that were asked for since the last call.
    /// </summary>
    public void Render(DrawingHandleScreen handle)
    {
        if (_pending.Count == 0)
            return;

        _target ??= _clyde.CreateRenderTarget(new Vector2i(Size, Size), new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb), name: "bar-dispenser-probe");
        var target = _target;
        foreach (var (uid, info) in _pending)
        {
            if (_entMan.Deleted(uid) || !_entMan.TryGetComponent(uid, out SpriteComponent? sprite))
                continue;

            handle.RenderInRenderTarget(target,
                () =>
                {
                    handle.SetTransform(Matrix3x2.Identity);
                    handle.DrawEntity(uid, new Vector2(Size / 2f), Vector2.One, Angle.Zero, overrideDirection: Direction.South, sprite: sprite);
                },
                Color.Transparent);
            var look = Look(uid);
            target.CopyPixelsToMemory<Rgba32>(image =>
            {
                using (image)
                {
                    Read(info, image);
                }

                if (_looks.Count >= Capacity)
                    _looks.Clear();

                var seen = new BarSpriteInfo();
                seen.CopyFrom(info);
                _looks[look] = seen;
            });
        }

        _pending.Clear();
    }

    private static void Read(BarSpriteInfo info, Image<Rgba32> image)
    {
        info.Measured = true;
        var span = image.GetPixelSpan();
        var stride = image.Width;
        int left = image.Width, top = image.Height, right = -1, bottom = -1;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                if (span[y * stride + x].A < AlphaThreshold)
                    continue;

                left = Math.Min(left, x);
                right = Math.Max(right, x);
                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y);
            }
        }

        if (right < 0)
            return;

        // The thing stands on the widest of its lowest rows.
        int footLeft = left, footRight = right, widest = -1;
        for (var y = Math.Max(top, bottom - FootRows + 1); y <= bottom; y++)
        {
            int rowLeft = image.Width, rowRight = -1;
            for (var x = left; x <= right; x++)
            {
                if (span[y * stride + x].A < AlphaThreshold)
                    continue;

                rowLeft = Math.Min(rowLeft, x);
                rowRight = Math.Max(rowRight, x);
            }

            if (rowRight - rowLeft <= widest)
                continue;

            widest = rowRight - rowLeft;
            footLeft = rowLeft;
            footRight = rowRight;
        }

        var half = image.Width / 2;
        info.Left = left - half;
        info.Right = right - half;
        info.Top = top - image.Height / 2;
        info.Bottom = bottom - image.Height / 2;
        info.Axis = (footLeft + footRight + 1) / 2f - half;
        info.FootEdge = footRight + 1 - half;
    }

    /// <summary>
    /// Lets go of the graphics memory. Pictures are measured again when somebody asks for them.
    /// </summary>
    public void Release()
    {
        _target?.Dispose();
        _target = null;
        _known.Clear();
        _looks.Clear();
        _pending.Clear();
    }
}
