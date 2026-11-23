using System.Numerics;
using Content.Client._Polonium.UserInterface.PixelUi;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client._Polonium.Chemistry.UI;

/// <summary>
/// Draws the sprites of entities on the device: turned, and faded when they come or go.
/// </summary>
public sealed class BarEntityPainter
{
    // Room for a 32 pixel sprite at twice its size, in art pixels.
    private const int PictureSize = 64;

    private readonly IEntityManager _entMan;
    private readonly IClyde _clyde;
    private readonly Action _drawPicture;
    private IRenderTexture? _picture;
    private DrawingHandleScreen? _handle;
    private SpriteComponent? _sprite;
    private EntityUid _entity;
    private Angle _turn;
    private int _spriteScale;
    private int _pictureScale;

    public BarEntityPainter(IEntityManager entMan, IClyde clyde)
    {
        _entMan = entMan;
        _clyde = clyde;
        _drawPicture = DrawPicture;
    }

    /// <summary>
    /// Draws the entity's sprite with its centre at the given point.
    /// </summary>
    /// <param name="spriteScale">Art pixels per pixel of the sprite.</param>
    /// <param name="turn">How far the sprite is turned clockwise.</param>
    public void Draw(DrawingHandleScreen handle, PixelCanvas canvas, EntityUid uid, int centerX, int centerY, int spriteScale, Angle turn, float alpha = 1f)
    {
        if (alpha <= 0f || _entMan.Deleted(uid) || !_entMan.TryGetComponent(uid, out SpriteComponent? sprite))
            return;

        if (alpha >= 1f)
        {
            var position = new Vector2(centerX * canvas.Scale, centerY * canvas.Scale);
            handle.DrawEntity(uid, position, new Vector2(spriteScale * canvas.Scale), -turn, overrideDirection: Direction.South, sprite: sprite);
            return;
        }

        // The engine draws an entity only fully opaque, so a sprite that fades goes through a picture of its own.
        if (_picture == null || _pictureScale != canvas.Scale)
        {
            _picture?.Dispose();
            _pictureScale = canvas.Scale;
            _picture = _clyde.CreateRenderTarget(
                new Vector2i(PictureSize, PictureSize) * canvas.Scale,
                new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb),
                name: "bar-dispenser-sprite");
        }

        _entity = uid;
        _sprite = sprite;
        _turn = turn;
        _spriteScale = spriteScale;
        _handle = handle;
        handle.RenderInRenderTarget(_picture, _drawPicture, Color.Transparent);
        _handle = null;
        _sprite = null;

        var source = UIBox2.FromDimensions(Vector2.Zero, _picture.Size);
        canvas.Stretch(_picture.Texture, source, centerX - PictureSize / 2, centerY - PictureSize / 2, PictureSize, PictureSize, Color.White.WithAlpha(alpha));
    }

    private void DrawPicture()
    {
        if (_handle is not { } handle || _picture == null)
            return;

        handle.SetTransform(Matrix3x2.Identity);
        handle.DrawEntity(_entity, (Vector2) _picture.Size / 2f, new Vector2(_spriteScale * _pictureScale), -_turn, overrideDirection: Direction.South, sprite: _sprite);
    }

    /// <summary>
    /// Lets go of the graphics memory.
    /// </summary>
    public void Release()
    {
        _picture?.Dispose();
        _picture = null;
    }
}
