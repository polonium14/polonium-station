using System;
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
///     body to move; drag a corner handle to scale from the opposite corner.
/// </summary>
public sealed class SignaturePlacementControl : Control
{
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

    // The signature box in this control's local pixels, as last arranged.
    private UIBox2i _boxRectPx;

    /// <summary>The signature box in this control's local pixels.</summary>
    public UIBox2i BoxRectPx => _boxRectPx;

    /// <summary>Fires whenever the box is moved or scaled (re-arranged).</summary>
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
    }

    private DragMode _drag = DragMode.None;
    private Vector2 _grabMousePx;
    private Vector2 _grabCenterPx;
    private float _grabScale;
    private Vector2i _grabOppositeCornerPx;
    private Vector2 _grabSignVec;
    private float _grabDiagPx;
    private Vector2 _grabDiagDir;

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
        var center = ClampCenter(_centerPx.Value, half, sizePx);
        _centerPx = center;
        var topLeft = center - half;
        var topLeftI = new Vector2i((int)topLeft.X, (int)topLeft.Y);
        var sizeI = new Vector2i((int)size.X, (int)size.Y);
        _preview.ArrangePixel(new UIBox2i(topLeftI, topLeftI + sizeI));
        SetBoxRect();
    }

    private Vector2 ClampCenter(Vector2 center, Vector2 half, Vector2 size)
    {
        var min = half;
        var max = size - half;
        var x = max.X > min.X ? Math.Clamp(center.X, min.X, max.X) : center.X;
        var y = max.Y > min.Y ? Math.Clamp(center.Y, min.Y, max.Y) : center.Y;
        return new Vector2(x, y);
    }

    private void LayoutScaledPinned()
    {
        if (_preview == null)
            return;

        var size = PreviewSizePx;
        var sizeI = new Vector2i((int)size.X, (int)size.Y);

        _preview.ArrangePixel(new UIBox2i(Vector2i.Zero, sizeI));
        var inkPos = _preview.InkLabelPosPx;
        var inkSize = _preview.InkLabelSizePx;
        var o = _grabOppositeCornerPx;

        var tlx = _grabSignVec.X > 0
            ? o.X - (int)inkPos.X
            : o.X - (int)inkPos.X - (int)inkSize.X;
        var tly = _grabSignVec.Y > 0
            ? o.Y - (int)inkPos.Y
            : o.Y - (int)inkPos.Y - (int)inkSize.Y;
        var topLeftI = ClampTopLeft(new Vector2i(tlx, tly), sizeI, CurrentSizePx);

        _preview.ArrangePixel(new UIBox2i(topLeftI, topLeftI + sizeI));
        _centerPx = new Vector2(topLeftI.X, topLeftI.Y) + new Vector2(sizeI.X, sizeI.Y) * 0.5f;
        SetBoxRect();
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

    private static Vector2i ClampTopLeft(Vector2i topLeft, Vector2i size, Vector2 screen)
    {
        var maxX = (int)screen.X - size.X;
        var maxY = (int)screen.Y - size.Y;
        var x = maxX > 0 ? Math.Clamp(topLeft.X, 0, maxX) : topLeft.X;
        var y = maxY > 0 ? Math.Clamp(topLeft.Y, 0, maxY) : topLeft.Y;
        return new Vector2i(x, y);
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

            var corners = CornersLocal();
            var opp = corners[3 - handleIdx];
            _grabOppositeCornerPx = new Vector2i((int)MathF.Round(opp.X), (int)MathF.Round(opp.Y));
            var diag = corners[handleIdx] - opp;
            _grabDiagPx = MathF.Max(1f, diag.Length());
            _grabDiagDir = diag / _grabDiagPx;
            _grabSignVec = new Vector2(diag.X >= 0 ? 1f : -1f, diag.Y >= 0 ? 1f : -1f);

            args.Handle();
            return;
        }

        var o = BoxOriginPx;
        var s = BoxSizePx;
        if (mouse.X >= o.X && mouse.X <= o.X + s.X && mouse.Y >= o.Y && mouse.Y <= o.Y + s.Y)
        {
            _drag = DragMode.Move;
            _grabMousePx = mouse;
            _grabCenterPx = BoxCenterLocal();
            args.Handle();
        }
    }

    private Vector2 BoxCenterLocal()
    {
        return new Vector2(_preview!.PixelPosition.X, _preview.PixelPosition.Y) + PreviewSizePx * 0.5f;
    }

    private Vector2 BoxOriginPx =>
        _preview == null ? Vector2.Zero
            : new Vector2(_preview.PixelPosition.X, _preview.PixelPosition.Y) + _preview.InkLabelPosPx;

    private Vector2 BoxSizePx => _preview == null ? Vector2.Zero : _preview.InkLabelSizePx;

    private Vector2[] CornersLocal()
    {
        var o = BoxOriginPx;
        var s = BoxSizePx;
        return new[]
        {
            o,
            o + new Vector2(s.X, 0),
            o + new Vector2(0, s.Y),
            o + s,
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
                var opp = new Vector2(_grabOppositeCornerPx.X, _grabOppositeCornerPx.Y);
                var proj = Vector2.Dot(mouse - opp, _grabDiagDir);
                var newScale = Math.Clamp(_grabScale * (proj / _grabDiagPx), MinScale, MaxScale);
                if (MathF.Abs(newScale - _scale) > 0.001f)
                {
                    _scale = newScale;
                    UpdatePreview();
                    MeasurePreview();
                    LayoutScaledPinned();
                }
                break;
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (_preview == null)
            return;

        var tl = BoxOriginPx;
        var size = BoxSizePx;
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
