using System.Numerics;
using Content.Client._Polonium.UserInterface.PixelUi;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Kitchen.UI;

/// <summary>
/// A round key of the blender in its chrome ring, with a pictogram on the cap.
/// The selector knob is the same part without a pictogram.
/// </summary>
public sealed class GrinderKey : BaseButton
{
    private static readonly Color Print = Color.FromHex("#f2f2f2");
    private static readonly Color DisabledPrint = Color.FromHex("#707070");

    public Texture? Atlas;
    public UIBox2 Face;
    public UIBox2 FaceDown;
    public UIBox2? Icon;

    /// <summary>
    /// Keeps the key down and its pictogram lit while the program it starts is running.
    /// </summary>
    public bool Latched;

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var shape = Disabled ? CursorShape.Arrow : CursorShape.Hand;
        if (DefaultCursorShape != shape)
            DefaultCursorShape = shape;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (Atlas == null)
            return;

        var canvas = new PixelCanvas(handle, UIScale);
        var pressed = Latched || DrawMode == DrawModeEnum.Pressed;
        canvas.Region(Atlas, pressed ? FaceDown : Face, 0, 0);
        if (Icon is not { } icon)
            return;

        var center = PixelSize.X / canvas.Scale / 2;
        var color = Latched ? GrinderArt.LampGreen : Disabled ? DisabledPrint : Print;
        canvas.Region(Atlas, icon, center - ((int) icon.Width - 1) / 2, center - ((int) icon.Height - 1) / 2 + (pressed ? 1 : 0), color);
    }

    protected override bool HasPoint(Vector2 point)
    {
        var radius = Size.X / 2f;
        return (point - new Vector2(radius)).LengthSquared() <= radius * radius;
    }
}
