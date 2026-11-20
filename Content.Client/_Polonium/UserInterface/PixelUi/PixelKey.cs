using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Polonium.UserInterface.PixelUi;

/// <summary>
/// A physical key drawn from a two-frame skin (raised, pressed). It sinks by one art pixel while held.
/// </summary>
public sealed class PixelKey : BaseButton
{
    private const int FrameWidth = 7;
    private const int FrameHeight = 8;
    private const int MarginLeft = 2;
    private const int MarginTop = 3;
    private const int MarginRight = 2;
    private const int MarginBottom = 4;
    private const int LineGap = 4;
    private const int LampSize = 2;
    private const float DisabledAlpha = 0.4f;

    private static readonly Color DisabledSkin = Color.FromHex("#c4c4c4");
    private static readonly UIBox2 RaisedFrame = UIBox2.FromDimensions(0f, 0f, FrameWidth, FrameHeight);
    private static readonly UIBox2 PressedFrame = UIBox2.FromDimensions(FrameWidth, 0f, FrameWidth, FrameHeight);

    private readonly List<(int Start, int Length, int Width)> _lines = new();
    private string _text = string.Empty;
    private int _wrappedWidth = -1;

    public Texture? Skin;
    public PixelFont Font;
    public Color TextColor = Color.FromHex("#2b2b2b");
    public Color LampOn = Color.FromHex("#30f050");
    public Color LampOff = Color.FromHex("#645951");

    /// <summary>
    /// Whether the key carries an indicator lamp and whether it is lit. Null means no lamp.
    /// </summary>
    public bool? Lamp;

    public bool Latched;

    public string Text
    {
        get => _text;
        set
        {
            _text = value;
            _wrappedWidth = -1;
        }
    }

    public PixelKey()
    {
        DefaultCursorShape = CursorShape.Hand;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (Skin == null)
            return;

        var canvas = new PixelCanvas(handle, UIScale);
        var width = PixelSize.X / canvas.Scale;
        var height = PixelSize.Y / canvas.Scale;
        if (width <= MarginLeft + MarginRight || height < FrameHeight)
            return;

        var pressed = Latched || DrawMode == DrawModeEnum.Pressed;
        var off = Disabled && !Latched;
        canvas.NineSlice(Skin, pressed ? PressedFrame : RaisedFrame, MarginLeft, MarginTop, MarginRight, MarginBottom, 0, 0, width, height, off ? DisabledSkin : null);

        var faceTop = pressed ? 2 : 1;
        var faceHeight = height - 4;
        if (Lamp is { } lit)
            canvas.Rect(width - 3 - LampSize, faceTop + 2, LampSize, LampSize, lit ? LampOn : LampOff);

        if (_text.Length == 0 || Font.Font == null)
            return;

        var faceWidth = width - 4;
        if (_wrappedWidth != faceWidth)
            Wrap(faceWidth);

        var color = off ? TextColor.WithAlpha(TextColor.A * DisabledAlpha) : TextColor;
        var block = _lines.Count * Font.CapHeight + (_lines.Count - 1) * LineGap;
        var y = faceTop + (faceHeight - block + 1) / 2;
        var shift = Lamp == null ? 0 : LampSize;
        foreach (var (start, length, lineWidth) in _lines)
        {
            canvas.Text(Font, (width - lineWidth) / 2 - shift, y - Font.CapTop, _text.AsSpan(start, length), color);
            y += Font.CapHeight + LineGap;
        }
    }

    private void Wrap(int width)
    {
        _wrappedWidth = width;
        _lines.Clear();

        var text = _text.AsSpan();
        var full = Font.Measure(text) - 1;
        var split = -1;
        if (full > width)
        {
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == ' ' && (split < 0 || Math.Abs(i - text.Length / 2) < Math.Abs(split - text.Length / 2)))
                    split = i;
            }
        }

        if (split < 0)
        {
            _lines.Add((0, text.Length, full));
            return;
        }

        _lines.Add((0, split, Font.Measure(text[..split]) - 1));
        _lines.Add((split + 1, text.Length - split - 1, Font.Measure(text[(split + 1)..]) - 1));
    }
}
