using System.Numerics;
using Content.Client._Polonium.UserInterface.PixelUi;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Chemistry.UI;

/// <summary>
/// A round key of the dispenser in its chrome ring, with a pictogram on the cap.
/// </summary>
public sealed class BarRoundKey : BaseButton
{
    private static readonly Color Print = Color.FromHex("#f2f2f2");
    private static readonly Color DisabledPrint = Color.FromHex("#707070");

    public Texture? Atlas;
    public UIBox2 Icon;

    /// <summary>
    /// Keeps the key down and its pictogram lit while the mode it switches is on.
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
        canvas.Region(Atlas, pressed ? BarDispenserArt.RoundKeyDown : BarDispenserArt.RoundKey, 0, 0);

        var center = PixelSize.X / canvas.Scale / 2;
        var color = Latched ? BarDispenserArt.Amber : Disabled ? DisabledPrint : Print;
        canvas.Region(Atlas, Icon, center - ((int) Icon.Width - 1) / 2, center - ((int) Icon.Height - 1) / 2 + (pressed ? 1 : 0), color);
    }

    protected override bool HasPoint(Vector2 point)
    {
        var radius = Size.X / 2f;
        return (point - new Vector2(radius)).LengthSquared() <= radius * radius;
    }
}
