// SPDX-FileCopyrightText: 2026 Copilot <175728472+Copilot@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Nikita (Nick) <174215049+nikitosych@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 Polonium-bot <admin@ss14.pl>
// SPDX-FileCopyrightText: 2026 nikitosych <174215049+nikitosych@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Client._Polonium.Tutorial.Lobby.UI;

/// <summary>
/// Overlay control that visually highlights UI elements by dimming the background and drawing a highlight
/// border around a specified control.
/// </summary>
public sealed class TutorialHighlightOverlay : Control
{
    public string Id { get; }
    public HighlightRegion? CurrentHighlight { get; private set; }
    public Color OverlayColor { get; }
    public bool IsSelfClosingOnClick { get; }
    public event Action? InternalOverlayClosedEvent;

    private readonly PanelContainer _background;
    private readonly LayoutContainer _content;
    private readonly List<PendingPlacement> _pendingPlacements = new();
    private readonly List<TrackedControl> _trackedControls = new();
    private readonly bool _ignoreClicks;
    private readonly bool _ignoreHighlightClicks;

    public TutorialHighlightOverlay(
        string id, 
        Control rootContainer, 
        Color? bgColor = null, 
        bool isSelfClosingOnClick = false, 
        bool ignoreClicks = true,
        bool ignoreHighlightClicks = false)
    {
        Id = id;
        _ignoreClicks = ignoreClicks;
        _ignoreHighlightClicks = ignoreHighlightClicks;

        VerticalExpand = true;
        HorizontalExpand = true;
        MouseFilter = ignoreClicks ? MouseFilterMode.Stop : MouseFilterMode.Pass;

        _background = new PanelContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
            MouseFilter = MouseFilterMode.Ignore,
        };
        
        AddChild(_background);

        _content = new LayoutContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
            MouseFilter = MouseFilterMode.Ignore,
        };

        AddChild(_content);

        OverlayColor = bgColor ?? Color.Black.WithAlpha(0.8f);
        IsSelfClosingOnClick = isSelfClosingOnClick;

        rootContainer.AddChild(this);
    }

    #region Core Methods

    /// <summary>
    /// Highlights the specified control by drawing a border around it using the given color and margin.
    /// </summary>
    /// <remarks>If the control is not visible or does not have a parent, the method does not apply a
    /// highlight. Only one control can be highlighted at a time, this method replaces any existing
    /// highlight.</remarks>
    /// <param name="control">Control to be highlighted. Must be visible in the control tree and have a parent.</param>
    /// <param name="highlightColor">Color to use for the highlight border.</param>
    /// <param name="margin">Margin in pixels to apply between the control and the highlight border. Defaults to 0.</param>
    /// <param name="closeOverlayOnClick">Whether the overlay should close when the highlighted control is clicked.</param>
    public void HighlightControl(Control control, Color highlightColor, float margin = 0f, bool closeOverlayOnClick = false)
    {
        if (!control.VisibleInTree || control.Parent == null)
            return;

        var region = new HighlightRegion
        {
            Control = control,
            Margin = margin,
            CloseOverlayOnClick = closeOverlayOnClick,
        };

        var highlightBox = new HighlightBorder
        {
            BorderColor = highlightColor,
            MouseFilter = MouseFilterMode.Ignore,
        };

        region.Highlight = highlightBox;
        _content.AddChild(highlightBox);

        if (_ignoreHighlightClicks)
        { // skoro nasza kontrolka jest w innej gałęzi drzewa UI,
          // najlepszym sposobem ignorowania kliknięć to ustawienie filtra bezpośrednio na HighlightRegion
            region.OriginalMouseFilter = control.MouseFilter;
            control.MouseFilter = MouseFilterMode.Ignore;
        }

        if (region is { CloseOverlayOnClick: true, Control: Button button })
        {
            button.OnPressed += _ => DestroyOverlay();
        }

        CurrentHighlight = region;

        UpdateHighlightPositions();
    }

    public void UnhighlightControl()
    {
        if (CurrentHighlight is { } region)
        {
            if (_ignoreHighlightClicks && region.OriginalMouseFilter is { } original)
            {
                region.Control.MouseFilter = original;
            }

            region.Highlight?.Orphan();
        }

        CurrentHighlight = null;
    }

    private void AddOverlayControl(Control control, Vector2? position = null)
    {
        _content.AddChild(control);

        if (position != null)
            LayoutContainer.SetPosition(control, position.Value / UIScale);
    }

    public void AddControlRelative(
        Control overlayControl,
        OverlayControlPosition relativePosition,
        float spacing = 100f,
        bool deferred = true
    )
    {
        if (spacing < 0)
            throw new ArgumentException("Specified argument cannot be negative number.", nameof(spacing));

        _content.AddChild(overlayControl);

        var tracked = new TrackedControl(overlayControl, null, relativePosition, spacing, UseOverlayBounds: true);
        _trackedControls.Add(tracked);

        if (!deferred && IsArrangeValid && PixelSize is { X: > 0, Y: > 0 })
        {
            if (!overlayControl.IsMeasureValid)
                overlayControl.Measure(Size);

            var controlSize = overlayControl.DesiredPixelSize;
            var position = CalculatePositionInContainer(relativePosition, controlSize, PixelSize, spacing);

            AddOverlayControl(overlayControl, position);
            return;
        }

        QueuePlacement(overlayControl, null, relativePosition, spacing, useOverlayBounds: true);
    }

    public void AddControlRelative(
        Control overlayControl,
        Control relativeToControl,
        OverlayControlPosition anchorPosition,
        float spacing = 10f,
        bool deferred = true)
    {
        if (spacing < 0)
            throw new ArgumentException("Specified argument cannot be negative number.", nameof(spacing));

        _content.AddChild(overlayControl);

        var tracked = new TrackedControl(overlayControl, relativeToControl, anchorPosition, spacing, UseOverlayBounds: false);
        _trackedControls.Add(tracked);

        if (!deferred && IsArrangeValid && relativeToControl is { IsArrangeValid: true, PixelSize: { X: > 0, Y: > 0 } })
        {
            if (!overlayControl.IsMeasureValid)
                overlayControl.Measure(Size);

            var relPos = relativeToControl.GlobalPixelPosition;
            var relSize = relativeToControl.PixelSize;
            var controlSize = overlayControl.DesiredPixelSize;

            var offset = CalculateOffsetRelativeToControl(anchorPosition, controlSize, relSize, spacing);

            AddOverlayControl(overlayControl, relPos + offset);
            return;
        }

        QueuePlacement(overlayControl, relativeToControl, anchorPosition, spacing, useOverlayBounds: false);
    }

    private void QueuePlacement(Control overlayControl,
        Control? relativeToControl,
        OverlayControlPosition posKind,
        float spacing,
        bool useOverlayBounds)
    {
        _pendingPlacements.Add(new PendingPlacement(overlayControl, relativeToControl, posKind, spacing, useOverlayBounds));
    }

    private void ProcessPendingPlacements()
    {
        if (_pendingPlacements.Count == 0)
            return;

        for (var i = _pendingPlacements.Count - 1; i >= 0; i--)
        {
            var p = _pendingPlacements[i];

            if (!IsArrangeValid || PixelSize.X == 0 || PixelSize.Y == 0)
                continue;

            if (!p.UseOverlayBounds &&
                (p.RelativeToControl is not { IsArrangeValid: true } rel ||
                 rel.PixelSize.X == 0 || rel.PixelSize.Y == 0))
            {
                continue;
            }

            if (!p.OverlayControl.IsMeasureValid)
                p.OverlayControl.Measure(Size);

            var controlSize = p.OverlayControl.DesiredPixelSize;
            Vector2 finalPos;

            if (p.UseOverlayBounds)
            {
                finalPos = CalculatePositionInContainer(p.PositionKind, controlSize, PixelSize, p.Spacing) / UIScale;
            }
            else
            {
                var relative = p.RelativeToControl!;
                var relPos = relative.GlobalPixelPosition;
                var relSize = relative.PixelSize;

                var offset = CalculateOffsetRelativeToControl(p.PositionKind, controlSize, relSize, p.Spacing);
                finalPos = (relPos + offset) / UIScale;
            }

            LayoutContainer.SetPosition(p.OverlayControl, finalPos);
            _pendingPlacements.RemoveAt(i);
        }
    }

    /// <summary>
    /// Updates the position and size of the highlight box to match current highlight control's location and
    /// dimensions.
    /// </summary>
    /// <remarks>This method recalculates the highlight box's placement and size based on the control's <see cref="Control.GlobalPixelPosition"/>,
    /// <see cref="Control.PixelSize"/> and the margin.</remarks>
    private void UpdateHighlightPositions()
    {
        if (CurrentHighlight?.Control?.Parent == null || !CurrentHighlight.Control.VisibleInTree)
            return;

        if (CurrentHighlight.Highlight == null)
            return;

        var globalPixelPos = CurrentHighlight.Control.GlobalPixelPosition;
        var pixelSize = CurrentHighlight.Control.PixelSize;
        var pixelMargin = CurrentHighlight.Margin * UIScale;

        LayoutContainer.SetPosition(CurrentHighlight.Highlight, (globalPixelPos - new Vector2(pixelMargin, pixelMargin)) / UIScale);
        CurrentHighlight.Highlight.SetSize = (pixelSize + new Vector2(pixelMargin * 2, pixelMargin * 2)) / UIScale;
    }

    private void UpdateTrackedControlsPositions()
    {
        if (_trackedControls.Count == 0)
            return;

        for (var i = _trackedControls.Count - 1; i >= 0; i--)
        {
            var tracked = _trackedControls[i];

            if (tracked.OverlayControl.Parent == null)
            {
                _trackedControls.RemoveAt(i);
                continue;
            }

            if (!IsArrangeValid || PixelSize.X == 0 || PixelSize.Y == 0)
                continue;

            if (!tracked.UseOverlayBounds &&
                (tracked.RelativeToControl is not { IsArrangeValid: true, VisibleInTree: true } rel ||
                 rel.PixelSize.X == 0 || rel.PixelSize.Y == 0))
            {
                continue;
            }

            if (!tracked.OverlayControl.IsMeasureValid)
                tracked.OverlayControl.Measure(Size);

            var controlSize = tracked.OverlayControl.DesiredPixelSize;
            Vector2 finalPos;

            if (tracked.UseOverlayBounds)
            {
                finalPos = CalculatePositionInContainer(tracked.PositionKind, controlSize, PixelSize, tracked.Spacing) / UIScale;
            }
            else
            {
                var relative = tracked.RelativeToControl!;
                var relPos = relative.GlobalPixelPosition;
                var relSize = relative.PixelSize;

                var offset = CalculateOffsetRelativeToControl(tracked.PositionKind, controlSize, relSize, tracked.Spacing);
                finalPos = (relPos + offset) / UIScale;
            }

            LayoutContainer.SetPosition(tracked.OverlayControl, finalPos);
        }
    }

    #endregion

    #region Misc
    public void DestroyOverlay()
    {
        //Orphan();
        InternalOverlayClosedEvent?.Invoke();
    }

    private static Vector2 CalculatePositionInContainer(
        OverlayControlPosition position,
        Vector2 controlSize,
        Vector2 containerSize,
        float spacing)
    {
        return position switch
        {
            OverlayControlPosition.TopLeft => new Vector2(spacing, spacing),
            OverlayControlPosition.TopCenter => new Vector2(Math.Max(spacing, (containerSize.X - controlSize.X) / 2f), spacing),
            OverlayControlPosition.TopRight => new Vector2(Math.Max(spacing, containerSize.X - controlSize.X - spacing), spacing),
            OverlayControlPosition.CenterLeft => new Vector2(spacing, Math.Max(spacing, (containerSize.Y - controlSize.Y) / 2f)),
            OverlayControlPosition.Center => new Vector2(Math.Max(spacing, (containerSize.X - controlSize.X) / 2f), Math.Max(spacing, (containerSize.Y - controlSize.Y) / 2f)),
            OverlayControlPosition.CenterRight => new Vector2(Math.Max(spacing, containerSize.X - controlSize.X - spacing), Math.Max(spacing, (containerSize.Y - controlSize.Y) / 2f)),
            OverlayControlPosition.BottomLeft => new Vector2(spacing, Math.Max(spacing, containerSize.Y - controlSize.Y - spacing)),
            OverlayControlPosition.BottomCenter => new Vector2(Math.Max(spacing, (containerSize.X - controlSize.X) / 2f), Math.Max(spacing, containerSize.Y - controlSize.Y - spacing)),
            OverlayControlPosition.BottomRight => new Vector2(Math.Max(spacing, containerSize.X - controlSize.X - spacing), Math.Max(spacing, containerSize.Y - controlSize.Y - spacing)),
            _ => throw new ArgumentOutOfRangeException(nameof(position), position, null),
        };
    }

    private static Vector2 CalculateOffsetRelativeToControl(
        OverlayControlPosition position,
        Vector2 controlSize,
        Vector2 relativeSize,
        float spacing)
    {
        return position switch
        {
            OverlayControlPosition.TopLeft => new Vector2(0f, -controlSize.Y - spacing),
            OverlayControlPosition.TopCenter => new Vector2(
                (relativeSize.X - controlSize.X) / 2f,
                -controlSize.Y - spacing),
            OverlayControlPosition.TopRight => new Vector2(
                relativeSize.X - controlSize.X,
                -controlSize.Y - spacing),
            OverlayControlPosition.CenterLeft => new Vector2(
                -controlSize.X - spacing,
                (relativeSize.Y - controlSize.Y) / 2f),
            OverlayControlPosition.Center => new Vector2(
                (relativeSize.X - controlSize.X) / 2f,
                (relativeSize.Y - controlSize.Y) / 2f),
            OverlayControlPosition.CenterRight => new Vector2(
                relativeSize.X + spacing,
                (relativeSize.Y - controlSize.Y) / 2f),
            OverlayControlPosition.BottomLeft => new Vector2(
                0f,
                relativeSize.Y + spacing),
            OverlayControlPosition.BottomCenter => new Vector2(
                (relativeSize.X - controlSize.X) / 2f,
                relativeSize.Y + spacing),
            OverlayControlPosition.BottomRight => new Vector2(
                relativeSize.X - controlSize.X,
                relativeSize.Y + spacing),
            _ => throw new ArgumentOutOfRangeException(nameof(position), position, null),
        };
    }

    #endregion

    #region Overrides

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        UpdateHighlightPositions();
        UpdateTrackedControlsPositions();
        ProcessPendingPlacements();
    }

    protected override bool HasPoint(Vector2 point)
    {
        if (!_ignoreClicks)
            return false;

        if (CurrentHighlight?.Control is not { VisibleInTree: true })
            return true;

        var pixelPoint = point * UIScale;

        var globalPixelPos = CurrentHighlight.Control.GlobalPixelPosition;
        var pixelSize = CurrentHighlight.Control.PixelSize;
        var pixelMargin = CurrentHighlight.Margin * UIScale;

        var clearRect = new UIBox2(
            globalPixelPos - new Vector2(pixelMargin, pixelMargin),
            globalPixelPos + pixelSize + new Vector2(pixelMargin, pixelMargin)
        );

        var overlayPixelPoint = pixelPoint + GlobalPixelPosition;

        if (clearRect.Contains(overlayPixelPoint))
            return _ignoreHighlightClicks; 

        return true;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        //var screenRect = new UIBox2(Vector2.Zero, PixelSize);
        var screenRect = new UIBox2(Parent!.GlobalPixelPosition, Parent!.PixelSize);

        if (CurrentHighlight is not { } highlight)
        {
            handle.DrawRect(screenRect, OverlayColor);
            base.Draw(handle);
            return;
        }

        var globalPixelPos = highlight.Control.GlobalPixelPosition;
        var pixelSize = highlight.Control.PixelSize;
        var pixelMargin = highlight.Margin * UIScale;

        var clearRect = new UIBox2(
            globalPixelPos - new Vector2(pixelMargin, pixelMargin),
            globalPixelPos + pixelSize + new Vector2(pixelMargin, pixelMargin)
        );

        if (clearRect.Top > screenRect.Top)
        {
            handle.DrawRect(
                new UIBox2(screenRect.Left, screenRect.Top, screenRect.Right, clearRect.Top),
                OverlayColor);
        }

        if (clearRect.Bottom < screenRect.Bottom)
        {
            handle.DrawRect(
                new UIBox2(screenRect.Left, clearRect.Bottom, screenRect.Right, screenRect.Bottom),
                OverlayColor);
        }

        if (clearRect.Left > screenRect.Left)
        {
            handle.DrawRect(
                new UIBox2(screenRect.Left, clearRect.Top, clearRect.Left, clearRect.Bottom),
                OverlayColor);
        }

        if (clearRect.Right < screenRect.Right)
        {
            handle.DrawRect(
                new UIBox2(clearRect.Right, clearRect.Top, screenRect.Right, clearRect.Bottom),
                OverlayColor);
        }

        base.Draw(handle);
    }

    // Overriding input to allow closing the overlay on click
    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (!IsSelfClosingOnClick || args.Function != EngineKeyFunctions.UIClick)
            return;

        DestroyOverlay();
        args.Handle();
    }

    #endregion

    #region Inner Types
    public enum OverlayControlPosition
    {
        TopLeft,
        TopCenter,
        TopRight,
        CenterLeft,
        Center,
        CenterRight,
        BottomLeft,
        BottomCenter,
        BottomRight,
    }

    public sealed class HighlightRegion
    {
        public required Control Control { get; set; }
        public float Margin { get; set; } = 0f;
        public Control? Highlight { get; set; }
        public bool CloseOverlayOnClick { get; set; } = false;
        public MouseFilterMode? OriginalMouseFilter { get; set; }
    }

    /// <summary>
    /// Represents a control that draws a rectangular highlight border around its bounds.
    /// </summary>
    /// <remarks>The border is rendered using the specified color and a fixed width. This control is typically
    /// used to visually emphasize or highlight UI elements by outlining them.</remarks>
    private sealed class HighlightBorder : Control
    {
        public Color BorderColor { get; init; } = Color.Yellow;
        private const float BorderWidth = 2f;

        protected override void Draw(DrawingHandleScreen handle)
        {
            var box = PixelSizeBox;

            // 4 sides of the border
            // Top
            handle.DrawRect(new UIBox2(box.Left, box.Top, box.Right, box.Top + BorderWidth), BorderColor);
            // Bottom
            handle.DrawRect(new UIBox2(box.Left, box.Bottom - BorderWidth, box.Right, box.Bottom), BorderColor);
            // Left
            handle.DrawRect(new UIBox2(box.Left, box.Top, box.Left + BorderWidth, box.Bottom), BorderColor);
            // Right
            handle.DrawRect(new UIBox2(box.Right - BorderWidth, box.Top, box.Right, box.Bottom), BorderColor);
        }
    }

    /// <summary>
    /// Represents a pending request to position an overlay control relative to another control, specifying placement
    /// options and spacing.
    /// </summary>
    /// <param name="OverlayControl">Control to be positioned. This control will be placed according to the specified parameters.</param>
    /// <param name="RelativeToControl">Control relative to which the overlay will be positioned. If <see langword="null"/>, the overlay is
    /// positioned independently.</param>
    /// <param name="PositionKind">Placement strategy to use for the overlay control indicating how it should be positioned relative to the target control.</param>
    /// <param name="Spacing">Amount of spacing in pixels to apply between the overlay and the target control. Cannot be negative</param>
    /// <param name="UseOverlayBounds">Whether the overlay's own bounds should be used when calculating its placement.</param>
    private sealed record PendingPlacement(
        Control OverlayControl,
        Control? RelativeToControl,
        OverlayControlPosition PositionKind,
        float Spacing,
        bool UseOverlayBounds);

    /// <summary>
    /// Represents a control that is tracked for automatic position updates.
    /// </summary>
    /// <param name="OverlayControl">Control being tracked.</param>
    /// <param name="RelativeToControl">Control relative to which the overlay is positioned. If <see langword="null"/>, positioned relative to overlay bounds.</param>
    /// <param name="PositionKind">Position strategy used for this control.</param>
    /// <param name="Spacing">Spacing in pixels between the control and its anchor.</param>
    /// <param name="UseOverlayBounds">Whether to use overlay bounds for positioning.</param>
    private sealed record TrackedControl(
        Control OverlayControl,
        Control? RelativeToControl,
        OverlayControlPosition PositionKind,
        float Spacing,
        bool UseOverlayBounds);

    #endregion
}

