using Content.Client._Polonium.UserInterface.PixelUi;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Polonium.Lathe.UI;

/// <summary>
/// The emergency stop. It can always be pressed, also when there is nothing to stop.
/// </summary>
public sealed class LatheStopButton : BaseButton
{
    public const int ArtSize = 40;

    private static readonly UIBox2 RaisedFrame = UIBox2.FromDimensions(0f, 0f, ArtSize, ArtSize);
    private static readonly UIBox2 PressedFrame = UIBox2.FromDimensions(ArtSize, 0f, ArtSize, ArtSize);

    private readonly Texture _texture;
    private readonly PixelFont _font;
    private readonly string _text;
    private readonly int _textWidth;

    public LatheStopButton(Texture texture, PixelFont font, string text)
    {
        _texture = texture;
        _font = font;
        _text = text;
        _textWidth = font.Measure(text) - 1;
        DefaultCursorShape = CursorShape.Hand;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var canvas = new PixelCanvas(handle, UIScale);
        var pressed = DrawMode == DrawModeEnum.Pressed;
        canvas.Region(_texture, pressed ? PressedFrame : RaisedFrame, 0, 0);
        var top = (ArtSize - _font.CapHeight) / 2 - _font.CapTop + (pressed ? 1 : 0);
        canvas.Text(_font, (ArtSize - _textWidth) / 2, top, _text, Color.White);
    }
}
