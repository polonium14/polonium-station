using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Polonium.UserInterface.PixelUi;

/// <summary>
/// An icon on the face of a <see cref="PixelKey"/>. Added as a child of the key, it sinks together with it.
/// </summary>
public sealed class PixelKeyIcon : Control
{
    private const float DisabledAlpha = 0.4f;

    public Texture? Texture;
    public UIBox2 Region;
    public Color Color = Color.White;

    public PixelKeyIcon()
    {
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (Texture == null || Parent is not PixelKey key)
            return;

        var canvas = new PixelCanvas(handle, UIScale);
        var width = PixelSize.X / canvas.Scale;
        var height = PixelSize.Y / canvas.Scale;
        var pressed = key.Latched || key.DrawMode == BaseButton.DrawModeEnum.Pressed;
        var faceTop = pressed ? 2 : 1;
        var faceHeight = height - 4;
        var color = key.Disabled && !key.Latched ? Color.WithAlpha(Color.A * DisabledAlpha) : Color;
        var x = (width - (int) Region.Width) / 2;
        var y = faceTop + (faceHeight - (int) Region.Height + 1) / 2;
        canvas.Region(Texture, Region, x, y, color);
    }
}
