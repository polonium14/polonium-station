using System.Numerics;
using Content.Shared.Paper;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Maths;

namespace Content.Client.Paper.UI;

/// <summary>
///     An overlay gizmo that lets the player position and scale a signature
///     preview inside a bounding box before committing it to a paper. Drag the
///     body to move; drag a corner handle to scale about the center.
/// </summary>
public sealed class SignaturePlacementControl : Control
{
    // Scale bounds mirror the server-side clamp in SignatureSystem.
    private const float MinScale = 0.25f;
    private const float MaxScale = 4.0f;
    private const int BaseFontSize = 40;

    // Drawn half-size of a corner handle at rest / when hovered, in virtual
    // pixels (scaled by UIScale on use).
    private const float HandleHalf = 6f;
    private const float HandleHoverHalf = 11f;

    // Hit radius for grabbing/hovering a handle. Deliberately larger than the
    // drawn handle so it's easy to click.
    private const float HandleHitHalf = 14f;

    private static readonly Color BoxColor = Color.FromHex("#3B7FDE");
    private static readonly Color BoxColorPressed = Color.FromHex("#8FBBF2");

    // Index of the handle currently under the mouse, or -1.
    private int _hoveredHandle = -1;

    // Index of the handle currently being dragged, or -1.
    private int _pressedHandle = -1;

    private StampWidget? _preview;
    private StampDisplayInfo _info;

    private float _scale = 1f;
    private int _appliedFontSize = -1;

    // Center of the signature box, in this control's local pixels. Null until
    // first arranged, at which point it defaults to the control's center.
    private Vector2? _centerPx;

    private enum DragMode
    {
        None,
        Move,
        Scale,
    }

    private DragMode _drag = DragMode.None;
    private Vector2 _grabMousePx;
    private Vector2 _grabCenterPx;
    private float _grabScale;
    private float _grabHalfDiagPx;

    public SignaturePlacementControl()
    {
        MouseFilter = MouseFilterMode.Stop;
        Visible = false;
    }

    /// <summary>
    ///     Starts placement of a fresh signature preview.
    /// </summary>
    public void Begin(StampDisplayInfo info)
    {
        _info = info;
        _scale = 1f;
        _centerPx = null;
        _drag = DragMode.None;
        _appliedFontSize = -1;

        if (_preview != null)
        {
            RemoveChild(_preview);
            _preview = null;
        }

        UpdatePreview();
        Visible = true;
        InvalidateArrange();
    }

    public void End()
    {
        Visible = false;
        _drag = DragMode.None;
    }

    /// <summary>
    ///     The chosen transform: normalized [0,1] position, scale multiplier and
    ///     rotation (radians).
    /// </summary>
    public (Vector2 Position, float Scale, float Rotation) GetResult()
    {
        var size = new Vector2(PixelSize.X, PixelSize.Y);
        var center = _centerPx ?? size * 0.5f;
        var normalized = size.X <= 0 || size.Y <= 0
            ? new Vector2(0.5f, 0.5f)
            : new Vector2(center.X / size.X, center.Y / size.Y);
        return (Vector2.Clamp(normalized, Vector2.Zero, Vector2.One), _scale, 0f);
    }

    private void UpdatePreview()
    {
        var fontSize = (int)MathF.Max(1f, BaseFontSize * _scale);
        if (_preview != null && fontSize == _appliedFontSize)
            return;

        if (_preview != null)
            RemoveChild(_preview);

        _appliedFontSize = fontSize;
        var info = _info;
        info.Scale = _scale;
        _preview = new StampWidget { StampInfo = info };
        AddChild(_preview);
    }

    private Vector2 PreviewSizePx =>
        _preview != null ? new Vector2(_preview.DesiredPixelSize.X, _preview.DesiredPixelSize.Y) : Vector2.Zero;

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        MeasurePreview();
        return Vector2.Zero;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        LayoutPreview(finalSize * UIScale);
        return finalSize;
    }

    private void MeasurePreview()
    {
        _preview?.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));
    }

    private Vector2 CurrentSizePx => new(PixelSize.X, PixelSize.Y);

    private void LayoutPreview(Vector2 sizePx)
    {
        if (_preview == null)
            return;

        if (_centerPx == null)
        {
            if (sizePx.X <= 0 || sizePx.Y <= 0)
                return;

            _centerPx = sizePx * 0.5f;
        }

        var size = PreviewSizePx;
        var half = size * 0.5f;
        var center = ClampCenter(_centerPx.Value, half, sizePx);
        _centerPx = center;
        var topLeft = center - half;
        var topLeftI = new Vector2i((int)topLeft.X, (int)topLeft.Y);
        var sizeI = new Vector2i((int)size.X, (int)size.Y);
        _preview.ArrangePixel(new UIBox2i(topLeftI, topLeftI + sizeI));
    }

    private Vector2 ClampCenter(Vector2 center, Vector2 half, Vector2 size)
    {
        var min = half;
        var max = size - half;
        var x = max.X > min.X ? Math.Clamp(center.X, min.X, max.X) : center.X;
        var y = max.Y > min.Y ? Math.Clamp(center.Y, min.Y, max.Y) : center.Y;
        return new Vector2(x, y);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick || _preview == null)
            return;

        var mouse = args.RelativePosition * UIScale;

        var handleIdx = HandleAt(mouse);
        if (handleIdx >= 0)
        {
            _drag = DragMode.Scale;
            _pressedHandle = handleIdx;
            _grabMousePx = mouse;
            _grabScale = _scale;
            _grabHalfDiagPx = MathF.Max(1f, (PreviewSizePx * 0.5f).Length());
            args.Handle();
            return;
        }

        var boxCenter = BoxCenterLocal();
        var half = PreviewSizePx * 0.5f;
        if (MathF.Abs(mouse.X - boxCenter.X) <= half.X && MathF.Abs(mouse.Y - boxCenter.Y) <= half.Y)
        {
            _drag = DragMode.Move;
            _grabMousePx = mouse;
            _grabCenterPx = boxCenter;
            args.Handle();
        }
    }

    private Vector2 BoxCenterLocal()
    {
        return new Vector2(_preview!.PixelPosition.X, _preview.PixelPosition.Y) + PreviewSizePx * 0.5f;
    }

    private Vector2[] CornersLocal()
    {
        var center = BoxCenterLocal();
        var half = PreviewSizePx * 0.5f;
        return new[]
        {
            center + new Vector2(-half.X, -half.Y),
            center + new Vector2(half.X, -half.Y),
            center + new Vector2(-half.X, half.Y),
            center + new Vector2(half.X, half.Y),
        };
    }

    private int HandleAt(Vector2 pointPx)
    {
        if (_preview == null)
            return -1;

        var hit = HandleHitHalf * UIScale;
        var corners = CornersLocal();
        for (var i = 0; i < corners.Length; i++)
        {
            if (MathF.Abs(pointPx.X - corners[i].X) <= hit && MathF.Abs(pointPx.Y - corners[i].Y) <= hit)
                return i;
        }

        return -1;
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function == EngineKeyFunctions.UIClick)
        {
            _drag = DragMode.None;
            _pressedHandle = -1;
        }
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        _hoveredHandle = -1;
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        var mouse = args.RelativePosition * UIScale;

        if (_drag == DragMode.None)
        {
            _hoveredHandle = HandleAt(mouse);
            return;
        }

        if (_centerPx == null)
            return;

        switch (_drag)
        {
            case DragMode.Move:
                _centerPx = _grabCenterPx + (mouse - _grabMousePx);
                LayoutPreview(CurrentSizePx);
                break;

            case DragMode.Scale:
                var dist = (mouse - _centerPx.Value).Length();
                var newScale = Math.Clamp(_grabScale * (dist / _grabHalfDiagPx), MinScale, MaxScale);
                if (MathF.Abs(newScale - _scale) > 0.001f)
                {
                    _scale = newScale;
                    UpdatePreview();
                    MeasurePreview();
                    LayoutPreview(CurrentSizePx);
                }
                break;
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (_preview == null)
            return;

        var tl = new Vector2(_preview.PixelPosition.X, _preview.PixelPosition.Y);
        var size = PreviewSizePx;
        var tr = tl + new Vector2(size.X, 0);
        var bl = tl + new Vector2(0, size.Y);
        var br = tl + size;

        handle.DrawLine(tl, tr, BoxColor);
        handle.DrawLine(tr, br, BoxColor);
        handle.DrawLine(br, bl, BoxColor);
        handle.DrawLine(bl, tl, BoxColor);

        var corners = new[] { tl, tr, bl, br };
        for (var i = 0; i < corners.Length; i++)
        {
            var grown = i == _hoveredHandle || i == _pressedHandle;
            var hh = (grown ? HandleHoverHalf : HandleHalf) * UIScale;
            var fill = i == _pressedHandle ? BoxColorPressed : BoxColor;
            var box = new UIBox2(corners[i] - new Vector2(hh, hh), corners[i] + new Vector2(hh, hh));
            handle.DrawRect(box, fill);
        }
    }
}
