using System;
using System.Numerics;
using Content.Shared.Paper;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.IoC;
using Robust.Shared.Maths;

namespace Content.Client.Paper.UI;

/// <summary>
///     An overlay gizmo that lets the player position, scale and rotate a
///     signature preview inside a bounding box before committing it to a paper.
///     Drag the body to move; drag a corner handle to scale about the center;
///     drag the round knob above the box to rotate about the center. Hold Shift
///     while rotating to snap; right-click the knob to reset rotation.
/// </summary>
public sealed partial class SignaturePlacementControl : Control
{
    [Dependency] private IInputManager _inputManager = default!;

    // Scale bounds mirror the server-side clamp in SignatureSystem.
    private const float MinScale = 0.25f;
    private const float MaxScale = 4.0f;
    private const int BaseFontSize = 40;

    private const float PreviewInkAlpha = 0.5f;

    // Drawn half-size of a corner handle at rest / when hovered, in virtual
    // pixels (scaled by UIScale on use).
    private const float HandleHalf = 6f;
    private const float HandleHoverHalf = 11f;

    // Hit radius for grabbing/hovering a handle. Deliberately larger than the
    // drawn handle so it's easy to click.
    private const float HandleHitHalf = 14f;

    private const float KnobRadius = 6f;
    private const float KnobHoverRadius = 9f;
    private const float KnobHitHalf = 13f;
    private const float KnobStalkVirtual = HandleHoverHalf + 16f;

    private static readonly float SnapStep = MathHelper.DegreesToRadians(15f);

    private static readonly Color BoxColor = Color.FromHex("#3B7FDE");
    private static readonly Color BoxColorPressed = Color.FromHex("#8FBBF2");

    // Index of the handle currently under the mouse, or -1.
    private int _hoveredHandle = -1;

    // Index of the handle currently being dragged, or -1.
    private int _pressedHandle = -1;

    private bool _knobHovered;
    private bool _knobPressed;

    private StampWidget? _preview;
    private StampDisplayInfo _info;

    private float _scale = 1f;
    private float _rotation;
    private int _appliedFontSize = -1;

    private bool _allowScale = true;

    // Center of the signature box, in this control's local pixels. Null until
    // first arranged, at which point it defaults to the control's center.
    private Vector2? _centerPx;

    // The signature box in this control's local pixels, as last arranged.
    private UIBox2i _boxRectPx;

    /// <summary>The signature box in this control's local pixels.</summary>
    public UIBox2i BoxRectPx => _boxRectPx;

    /// <summary>Fires whenever the box is moved, scaled or rotated (re-arranged).</summary>
    public event Action? LayoutChanged;

    /// <summary>
    ///     How far, in virtual pixels, a corner handle extends past the box
    ///     outline (uses the grown/hover size so buttons clear it in every state).
    /// </summary>
    public float HandleExtentVirtual => HandleHoverHalf;

    private enum DragMode
    {
        None,
        Move,
        Scale,
        Rotate,
    }

    private DragMode _drag = DragMode.None;

    private Vector2 _grabMousePx;
    private Vector2 _grabBoxCenter;

    private Vector2 _grabCenterPx;
    private float _grabScale;
    private float _grabDist;
    private float _grabMaxFit;
    private float _grabRotation;
    private float _grabPointerAngle;

    public SignaturePlacementControl()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Stop;
        Visible = false;
    }

    /// <summary>
    ///     Starts placement of a fresh signature preview.
    /// </summary>
    public void Begin(StampDisplayInfo info, bool allowScale = true)
    {
        _info = info;
        _allowScale = allowScale;
        _scale = 1f;
        _rotation = 0f;
        _centerPx = null;
        _drag = DragMode.None;
        _appliedFontSize = -1;
        _pressedHandle = -1;
        _hoveredHandle = -1;
        _knobPressed = false;
        _knobHovered = false;

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
        _pressedHandle = -1;
        _knobPressed = false;
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
        return (Vector2.Clamp(normalized, Vector2.Zero, Vector2.One), _scale, NormalizeAngle(_rotation));
    }

    private void UpdatePreview()
    {
        var fontSize = (int)MathF.Max(1f, BaseFontSize * _scale);
        if (_preview != null && fontSize == _appliedFontSize)
        {
            _preview.Orientation = _rotation;
            return;
        }

        if (_preview != null)
            RemoveChild(_preview);

        _appliedFontSize = fontSize;
        var info = _info;
        info.Scale = _scale;
        info.Rotation = _rotation;
        _preview = new StampWidget { StampInfo = info };
        _preview.Orientation = _rotation;
        _preview.Modulate = Color.White.WithAlpha(PreviewInkAlpha);
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

        // Provisional arrange at the requested center so BoxOriginPx/BoxSizePx
        // reflect the current ink inset, then clamp the center so the (rotated)
        // corner scale handles stay within the control, and re-arrange.
        ArrangePreviewAt(_centerPx.Value - half, size);
        var center = ClampForHandles(_centerPx.Value, sizePx);
        _centerPx = center;
        ArrangePreviewAt(center - half, size);
        SetBoxRect();
    }

    // The largest scale at which the ink box plus handle reach still fits inside
    // the control at ANY rotation. Sized to the box's bounding circle (diagonal)
    // rather than the current-rotation AABB, so the cap is rotation-invariant:
    // scaling up at 45deg then resetting to 0 can't push the box off-canvas.
    private float MaxFitScale()
    {
        var ink = BoxSizePx;
        if (ink.X <= 0 || ink.Y <= 0)
            return MaxScale;

        var diameter = MathF.Sqrt(ink.X * ink.X + ink.Y * ink.Y);
        if (diameter <= 0)
            return MaxScale;

        var avail = CurrentSizePx - new Vector2(2f * HandleHoverHalf * UIScale);
        var fit = MathF.Min(avail.X, avail.Y) / diameter;
        return MathF.Max(MinScale, _scale * fit);
    }

    private void ArrangePreviewAt(Vector2 topLeft, Vector2 size)
    {
        var topLeftI = new Vector2i((int)topLeft.X, (int)topLeft.Y);
        var sizeI = new Vector2i((int)size.X, (int)size.Y);
        _preview!.ArrangePixel(new UIBox2i(topLeftI, topLeftI + sizeI));
    }

    // Clamps the widget center so the ink box's four rotated corner handles
    // (each expanded by the handle's drawn reach) stay within [0, controlSize].
    // Slides the box inward without changing its scale; if the box is too big to
    // fit on an axis, centers it there. Requires _preview to be arranged.
    private Vector2 ClampForHandles(Vector2 widgetCenter, Vector2 controlSize)
    {
        var inkSize = BoxSizePx;
        if (inkSize.X <= 0 || inkSize.Y <= 0)
            return widgetCenter;

        // Ink center relative to the widget center (constant for this scale).
        var offset = BoxCenterPx - widgetCenter;

        // Rotation-aware half-extent of the ink box plus the handle's reach.
        var boxHalf = inkSize * 0.5f;
        var cos = MathF.Abs(MathF.Cos(_rotation));
        var sin = MathF.Abs(MathF.Sin(_rotation));
        var handleReach = _allowScale ? HandleHoverHalf * UIScale : 0f;
        var margin = new Vector2(boxHalf.X * cos + boxHalf.Y * sin, boxHalf.X * sin + boxHalf.Y * cos)
            + new Vector2(handleReach);

        var lo = margin;
        var hi = controlSize - margin;
        var ink = BoxCenterPx;
        var x = hi.X > lo.X ? Math.Clamp(ink.X, lo.X, hi.X) : controlSize.X * 0.5f;
        var y = hi.Y > lo.Y ? Math.Clamp(ink.Y, lo.Y, hi.Y) : controlSize.Y * 0.5f;
        return new Vector2(x, y) - offset;
    }

    private void SetBoxRect()
    {
        var o = BoxOriginPx;
        var s = BoxSizePx;
        var topLeftI = new Vector2i((int)o.X, (int)o.Y);
        var sizeI = new Vector2i((int)s.X, (int)s.Y);
        _boxRectPx = new UIBox2i(topLeftI, topLeftI + sizeI);
        LayoutChanged?.Invoke();
    }

    private bool BoxWrapsWidget => _info.HasIcon;

    private Vector2 BoxOriginPx =>
        _preview == null ? Vector2.Zero
            : BoxWrapsWidget
                ? new Vector2(_preview.PixelPosition.X, _preview.PixelPosition.Y)
                : new Vector2(_preview.PixelPosition.X, _preview.PixelPosition.Y) + _preview.InkLabelPosPx;

    private Vector2 BoxSizePx =>
        _preview == null ? Vector2.Zero
            : BoxWrapsWidget
                ? new Vector2(_preview.PixelSize.X, _preview.PixelSize.Y)
                : _preview.InkLabelSizePx;

    private Vector2 BoxCenterPx => BoxOriginPx + BoxSizePx * 0.5f;

    private static Vector2 Rotate(Vector2 v, float angle)
    {
        var cos = MathF.Cos(angle);
        var sin = MathF.Sin(angle);
        return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }

    private Vector2[] RotatedCornersPx()
    {
        var o = BoxOriginPx;
        var s = BoxSizePx;
        var c = o + s * 0.5f;
        var pts = new[]
        {
            o,
            o + new Vector2(s.X, 0),
            o + new Vector2(0, s.Y),
            o + s,
        };
        for (var i = 0; i < pts.Length; i++)
            pts[i] = c + Rotate(pts[i] - c, _rotation);
        return pts;
    }

    private Vector2 KnobCenterPx()
    {
        var o = BoxOriginPx;
        var s = BoxSizePx;
        var c = o + s * 0.5f;
        var topMid = new Vector2(o.X + s.X * 0.5f, o.Y - KnobStalkVirtual * UIScale);
        return c + Rotate(topMid - c, _rotation);
    }

    private int HandleAt(Vector2 pointPx)
    {
        if (_preview == null || !_allowScale)
            return -1;

        var hit = HandleHitHalf * UIScale;
        var corners = RotatedCornersPx();
        for (var i = 0; i < corners.Length; i++)
        {
            if (MathF.Abs(pointPx.X - corners[i].X) <= hit && MathF.Abs(pointPx.Y - corners[i].Y) <= hit)
                return i;
        }

        return -1;
    }

    private bool KnobAt(Vector2 pointPx)
    {
        if (_preview == null)
            return false;

        return (pointPx - KnobCenterPx()).Length() <= KnobHitHalf * UIScale;
    }

    private bool PointInRotatedBox(Vector2 pointPx)
    {
        var c = BoxCenterPx;
        var local = c + Rotate(pointPx - c, -_rotation);
        var o = BoxOriginPx;
        var s = BoxSizePx;
        return local.X >= o.X && local.X <= o.X + s.X && local.Y >= o.Y && local.Y <= o.Y + s.Y;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (_preview == null)
            return;

        var mouse = args.RelativePosition * UIScale;

        // Right-click the knob resets rotation to zero.
        if (args.Function == EngineKeyFunctions.UIRightClick)
        {
            if (KnobAt(mouse))
            {
                _rotation = 0f;
                UpdatePreview();
                LayoutPreview(CurrentSizePx);
                args.Handle();
            }
            return;
        }

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        if (KnobAt(mouse))
        {
            _drag = DragMode.Rotate;
            _knobPressed = true;
            _grabCenterPx = BoxCenterPx;
            _grabRotation = _rotation;
            _grabPointerAngle = MathF.Atan2(mouse.Y - _grabCenterPx.Y, mouse.X - _grabCenterPx.X);
            args.Handle();
            return;
        }

        var handleIdx = HandleAt(mouse);
        if (handleIdx >= 0)
        {
            _drag = DragMode.Scale;
            _pressedHandle = handleIdx;
            _grabCenterPx = _centerPx ?? BoxCenterPx;
            _grabScale = _scale;
            _grabDist = MathF.Max(1f, (mouse - _grabCenterPx).Length());
            _grabMaxFit = MaxFitScale();
            args.Handle();
            return;
        }

        if (PointInRotatedBox(mouse))
        {
            _drag = DragMode.Move;
            _grabMousePx = mouse;
            _grabBoxCenter = _centerPx ?? BoxCenterPx;
            args.Handle();
        }
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function == EngineKeyFunctions.UIClick)
        {
            _drag = DragMode.None;
            _pressedHandle = -1;
            _knobPressed = false;
        }
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        _hoveredHandle = -1;
        _knobHovered = false;
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        var mouse = args.RelativePosition * UIScale;

        if (_drag == DragMode.None)
        {
            _knobHovered = KnobAt(mouse);
            _hoveredHandle = _knobHovered ? -1 : HandleAt(mouse);
            return;
        }

        if (_centerPx == null)
            return;

        switch (_drag)
        {
            case DragMode.Move:
                _centerPx = _grabBoxCenter + (mouse - _grabMousePx);
                LayoutPreview(CurrentSizePx);
                break;

            case DragMode.Scale:
                var dist = (mouse - _grabCenterPx).Length();
                var target = Math.Clamp(_grabScale * (dist / _grabDist), MinScale, MaxScale);
                var newScale = MathF.Max(MinScale, MathF.Min(target, _grabMaxFit));
                if (MathF.Abs(newScale - _scale) > 0.001f)
                {
                    _scale = newScale;
                    UpdatePreview();
                    MeasurePreview();
                    LayoutPreview(CurrentSizePx);
                }
                break;

            case DragMode.Rotate:
                var pointerAngle = MathF.Atan2(mouse.Y - _grabCenterPx.Y, mouse.X - _grabCenterPx.X);
                var rotation = _grabRotation + (pointerAngle - _grabPointerAngle);
                if (_inputManager.IsKeyDown(Keyboard.Key.Shift))
                    rotation = MathF.Round(rotation / SnapStep) * SnapStep;
                if (MathF.Abs(rotation - _rotation) > 0.0001f)
                {
                    _rotation = rotation;
                    UpdatePreview();
                    // Re-layout (not just SetBoxRect): rotating swings the corner
                    // handles out, so the center may need to slide to keep them in.
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

        var corners = RotatedCornersPx();
        var tl = corners[0];
        var tr = corners[1];
        var bl = corners[2];
        var br = corners[3];

        handle.DrawLine(tl, tr, BoxColor);
        handle.DrawLine(tr, br, BoxColor);
        handle.DrawLine(br, bl, BoxColor);
        handle.DrawLine(bl, tl, BoxColor);

        if (_allowScale)
        {
            for (var i = 0; i < corners.Length; i++)
            {
                var grown = i == _hoveredHandle || i == _pressedHandle;
                var hh = (grown ? HandleHoverHalf : HandleHalf) * UIScale;
                var fill = i == _pressedHandle ? BoxColorPressed : BoxColor;
                var box = new UIBox2(corners[i] - new Vector2(hh, hh), corners[i] + new Vector2(hh, hh));
                handle.DrawRect(box, fill);
            }
        }

        var topMid = (tl + tr) * 0.5f;
        var knob = KnobCenterPx();
        handle.DrawLine(topMid, knob, BoxColor);
        var kr = (_knobPressed || _knobHovered ? KnobHoverRadius : KnobRadius) * UIScale;
        var kfill = _knobPressed ? BoxColorPressed : BoxColor;
        handle.DrawCircle(knob, kr, kfill);
    }

    private static float NormalizeAngle(float angle)
    {
        var twoPi = MathF.PI * 2f;
        angle %= twoPi;
        if (angle > MathF.PI)
            angle -= twoPi;
        else if (angle < -MathF.PI)
            angle += twoPi;
        return angle;
    }
}
