using Content.Client._Polonium.UserInterface.PixelUi;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Disposal.UI;

/// <summary>
/// An invisible key over a part of the device picture. The window paints the part and asks this control whether it is held.
/// </summary>
public sealed class DisposalDeviceKey : BaseButton
{
    /// <summary>
    /// Raised when the mouse wheel turns over the key: -1 up the list, 1 down the list.
    /// </summary>
    public event Action<int>? OnWheel;

    public bool Held => DrawMode == DrawModeEnum.Pressed;

    public bool Hovered => DrawMode == DrawModeEnum.Hover;

    public DisposalDeviceKey()
    {
        DefaultCursorShape = CursorShape.Hand;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var shape = Disabled ? CursorShape.Arrow : CursorShape.Hand;
        if (DefaultCursorShape != shape)
            DefaultCursorShape = shape;
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);

        if (OnWheel == null || args.Delta.Y == 0)
            return;

        OnWheel(args.Delta.Y > 0 ? -1 : 1);
        args.Handle();
    }
}

/// <summary>
/// The grip of a lever. It reports how far the pointer has travelled, in art pixels, while the player holds it.
/// </summary>
public sealed class DisposalDeviceGrip : Control
{
    private float _start;

    public bool Grabbed { get; private set; }

    public event Action? OnGrab;
    public event Action<float>? OnDrag;
    public event Action? OnRelease;

    public DisposalDeviceGrip()
    {
        MouseFilter = MouseFilterMode.Stop;
        DefaultCursorShape = CursorShape.Hand;
    }

    /// <summary>
    /// Takes the lever out of the player's hand without counting it as a release.
    /// </summary>
    public void Drop()
    {
        Grabbed = false;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _start = args.RelativePixelPosition.Y;
        Grabbed = true;
        OnGrab?.Invoke();
        args.Handle();
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        if (Grabbed)
            OnDrag?.Invoke((args.RelativePixelPosition.Y - _start) / PixelCanvas.GetScale(UIScale));
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function != EngineKeyFunctions.UIClick || !Grabbed)
            return;

        Grabbed = false;
        OnRelease?.Invoke();
    }
}

/// <summary>
/// A part of the device drawn on top of the body and cut off at the edges of the control.
/// </summary>
public sealed class DisposalDeviceLayer : Control
{
    private readonly Action<PixelCanvas, int, int> _paint;

    /// <summary>
    /// The place of the layer in the window, in art pixels.
    /// </summary>
    public readonly UIBox2i Art;

    private readonly int _bodyTop;

    /// <param name="paint">Draws the layer. It gets the offset from the coordinates of the body to those of the layer.</param>
    public DisposalDeviceLayer(UIBox2i art, int bodyTop, Action<PixelCanvas, int, int> paint)
    {
        Art = art;
        _bodyTop = bodyTop;
        _paint = paint;
        RectClipContent = true;
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (PixelSize.X > 0 && PixelSize.Y > 0)
            _paint(new PixelCanvas(handle, UIScale), -Art.Left, _bodyTop - Art.Top);
    }
}
