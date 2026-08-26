using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client.Outline;
using Content.Shared._Polonium.Photography;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.Utility;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client._Polonium.Photography;

/// <summary>
/// Hidden control that renders queued photo captures. Render-to-texture + pixel readback can only happen inside the render loop, so the work runs in <see cref="Draw"/> - the same trick <c>ContentSpriteSystem</c> uses.
/// Rendered from the PLAYER's eye with FOV on, then cropped to a <see cref="PhotographyConstants.PhotoSizePixels"/>-square around the target, so the camera sees exactly what the player sees (walls occlude, glass shows through) and never ends up "inside" a wall the way an eye at the subject would.
/// Flash captures spawn a client-only point light at the target just before <c>Render()</c> and delete it right after; it lands this frame because the light-tree query flushes pending inserts before rendering and <c>Render()</c> is synchronous.
/// </summary>
public sealed partial class PhotographyCaptureControl : Control, IDisposable
{
    private const int Crop = PhotographyConstants.PhotoSizePixels;
    private const int Tile = PhotographyConstants.PixelsPerTile;

    // Render-square sizing. The player sits at the render centre and FOV coverage scales
    // with the viewport size (maxDist = size / 32 tiles), so the square must reach from
    // the player out to the target plus a margin for the crop. Sized per shot (bucketed
    // to a few sizes so the viewport isn't rebuilt every capture) to keep close shots
    // cheap and FOV crisp, capped at what PhotoMaxRange needs.
    private const int SizeStep = 256;
    private static readonly int MaxRender =
        RoundUp(((int) PhotographyConstants.PhotoMaxRange + 3) * 2 * Tile, SizeStep);

    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IEyeManager _eyeManager = default!;

    private readonly IClyde _clyde;
    private readonly Action<int, Image<Rgba32>> _onCaptured;

    private readonly Queue<(int CaptureId, MapCoordinates Target, bool Flash)> _queue = new();

    // Reused across captures; created lazily on the render thread.
    private IClydeViewport? _viewport;
    private readonly FixedEye _eye = new();
    private bool _disposed;

    public PhotographyCaptureControl(IClyde clyde, Action<int, Image<Rgba32>> onCaptured)
    {
        IoCManager.InjectDependencies(this);
        _clyde = clyde;
        _onCaptured = onCaptured;
        // Render with FOV so the photo occludes exactly like vision does: walls and closed
        // doors block, transparent glass / open doors don't. Zoom 1 keeps the photo at a
        // fixed 32 px/tile regardless of the player's own zoom.
        _eye.DrawFov = true;
        _eye.Zoom = Vector2.One;
    }

    public void Enqueue(int captureId, MapCoordinates target, bool flash)
    {
        _queue.Enqueue((captureId, target, flash));
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        while (_queue.TryDequeue(out var job))
        {
            // Shoot from the player's own eye, so the photo matches their view exactly.
            var playerEye = _eyeManager.CurrentEye;
            var playerPos = playerEye.Position;
            if (playerPos.MapId == MapId.Nullspace || playerPos.MapId != job.Target.MapId)
                continue;

            var renderSize = RenderSizeFor(playerPos.Position, job.Target.Position);
            if (_viewport == null || _viewport.Size.X != renderSize)
            {
                _viewport?.Dispose();
                _viewport = _clyde.CreateViewport(new Vector2i(renderSize, renderSize), name: "photo-capture");
            }

            _eye.Position = playerPos;
            _eye.Rotation = playerEye.Rotation;
            _viewport.Eye = _eye;

            // Strip the hover outline for this frame so the entity you're aiming at isn't
            // baked into the photo with a selection highlight; re-applied next FrameUpdate.
            // Restore the PRIOR value, not a hardcoded true, so we don't clobber another
            // system (e.g. drag-drop) that's currently holding the outline disabled.
            var outline = _entMan.System<InteractionOutlineSystem>();
            var outlineWasEnabled = outline.Enabled;
            outline.SetEnabled(false);

            // Client-only flash light at the subject, alive only for this render. try/finally
            // so a throw during Render can't leak the light (or the outline) permanently.
            var light = job.Flash ? SpawnFlashLight(job.Target) : (EntityUid?) null;
            try
            {
                _viewport.Render();
            }
            finally
            {
                if (light is { } lightUid)
                    _entMan.DeleteEntity(lightUid);
                outline.SetEnabled(outlineWasEnabled);
            }

            // The engine's CopyPixelsToMemory subRegion only clamps size (it always reads
            // from the corner), so read the whole render and crop the target window in CPU.
            var pixel = _viewport.WorldToLocal(job.Target.Position);
            var captureId = job.CaptureId;
            _viewport.RenderTarget.CopyPixelsToMemory<Rgba32>(image =>
            {
                // Dispose the (viewport-sized) source image the engine hands us either way;
                // skip the submit if we've been torn down since the readback was queued.
                using (image)
                {
                    if (_disposed)
                        return;

                    using var cropped = CropAround(image, pixel);
                    _onCaptured(captureId, cropped);
                }
            });
        }
    }

    // Big enough to hold the player (centre) and the target plus a crop margin, bucketed.
    private static int RenderSizeFor(Vector2 player, Vector2 target)
    {
        var distTiles = (target - player).Length();
        var neededPx = ((int) MathF.Ceiling(distTiles) + 3) * 2 * Tile;
        return Math.Clamp(RoundUp(neededPx, SizeStep), SizeStep, MaxRender);
    }

    // Copy the Crop-sized window centred on <paramref name="pixel"/> out of the full
    // render, clamped so an edge target still yields a full frame the codec can pack.
    private static Image<Rgba32> CropAround(Image<Rgba32> src, Vector2 pixel)
    {
        var origin = CropOrigin(pixel, src.Width, src.Height);

        var dst = new Image<Rgba32>(Crop, Crop);
        var s = src.GetPixelSpan();
        var d = dst.GetPixelSpan();
        for (var row = 0; row < Crop; row++)
        {
            var srcRow = (origin.Y + row) * src.Width + origin.X;
            s.Slice(srcRow, Crop).CopyTo(d.Slice(row * Crop, Crop));
        }

        return dst;
    }

    /// <summary>Top-left origin of the Crop-sized window centred on <paramref name="pixel"/>, clamped to stay fully inside a <paramref name="width"/> x <paramref name="height"/> image (never negative). Pure geometry - split out so it can be unit-tested without a render.</summary>
    public static Vector2i CropOrigin(Vector2 pixel, int width, int height)
    {
        const int half = Crop / 2;
        var x0 = Math.Clamp((int) MathF.Round(pixel.X) - half, 0, Math.Max(0, width - Crop));
        var y0 = Math.Clamp((int) MathF.Round(pixel.Y) - half, 0, Math.Max(0, height - Crop));
        return new Vector2i(x0, y0);
    }

    private static int RoundUp(int value, int step)
    {
        return (value + step - 1) / step * step;
    }

    private EntityUid SpawnFlashLight(MapCoordinates coords)
    {
        var light = _entMan.Spawn(null, coords);
        PhotoFlash.Configure(_entMan.System<SharedPointLightSystem>(), light);
        return light;
    }

    public new void Dispose()
    {
        _disposed = true;
        _queue.Clear();
        _viewport?.Dispose();
        _viewport = null;
    }
}
