using System.Numerics;
using System.Text;
using Content.Client._Polonium.UserInterface.PixelUi;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Lathe.UI;

/// <summary>
/// A one-line entry on a screen, typed like on a terminal: the cursor always stands at the end.
/// </summary>
public sealed partial class LatheTextField : Control
{
    [Dependency] private IClipboardManager _clipboard = default!;

    private const float BlinkTime = 0.53f;

    private string _text = string.Empty;
    private float _blink;

    public LatheScreenFont? Font;
    public Color TextColor = LathePalette.ScreenInput;
    public Color PlaceholderColor = LathePalette.ScreenLow;
    public string Placeholder = string.Empty;
    public int MaxLength = 32;
    public bool DigitsOnly;

    /// <summary>
    /// Large digits pushed to the right edge, one art pixel per pixel of the coarse font.
    /// </summary>
    public bool Readout;

    public event Action<string>? OnTextChanged;
    public event Action<int>? OnStep;

    public string Text => _text;

    public LatheTextField()
    {
        IoCManager.InjectDependencies(this);

        MouseFilter = MouseFilterMode.Stop;
        CanKeyboardFocus = true;
        KeyboardFocusOnClick = true;
        DefaultCursorShape = CursorShape.IBeam;
    }

    public void SetText(string text)
    {
        _text = text;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        _blink = (_blink + args.DeltaSeconds) % (BlinkTime * 2f);
    }

    protected override void KeyboardFocusEntered()
    {
        base.KeyboardFocusEntered();

        _blink = 0f;
        Root?.Window?.TextInputStart();
    }

    protected override void KeyboardFocusExited()
    {
        base.KeyboardFocusExited();

        Root?.Window?.TextInputStop();
    }

    protected override void TextEntered(GUITextEnteredEventArgs args)
    {
        base.TextEntered(args);

        Insert(args.Text);
    }

    private void Insert(string entered)
    {
        // The sandbox does not allow what the compiler makes of "string + char", hence the builder.
        var text = new StringBuilder(_text);
        foreach (var rune in entered.EnumerateRunes())
        {
            if (text.Length + rune.Utf16SequenceLength > MaxLength)
                break;

            if (Rune.IsControl(rune) || DigitsOnly && rune.Value is < '0' or > '9')
                continue;

            text.Append(rune.ToString());
        }

        Change(text.ToString());
    }

    private async void Paste()
    {
        var pasted = await _clipboard.GetText();
        if (!Disposed)
            Insert(pasted);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Handled)
            return;

        if (args.Function == EngineKeyFunctions.TextBackspace)
        {
            if (_text.Length > 0)
                Change(_text[..^(_text.Length > 1 && char.IsLowSurrogate(_text[^1]) ? 2 : 1)]);
        }
        else if (args.Function == EngineKeyFunctions.TextPaste)
        {
            Paste();
        }
        else if (args.Function == EngineKeyFunctions.TextSubmit || args.Function == EngineKeyFunctions.TextReleaseFocus)
        {
            ReleaseKeyboardFocus();
        }
        else if (args.Function == EngineKeyFunctions.TextHistoryPrev)
        {
            OnStep?.Invoke(1);
        }
        else if (args.Function == EngineKeyFunctions.TextHistoryNext)
        {
            OnStep?.Invoke(-1);
        }
        else
        {
            return;
        }

        args.Handle();
    }

    private void Change(string text)
    {
        if (text == _text)
            return;

        _text = text;
        _blink = 0f;
        OnTextChanged?.Invoke(text);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (Font == null || PixelSize.X <= 0 || PixelSize.Y <= 0)
            return;

        var scale = PixelCanvas.GetScale(UIScale);
        var face = Readout ? Font.Readout(scale) : Font.Face(scale);
        var dot = face.Dot;
        var focused = HasKeyboardFocus();
        var empty = _text.Length == 0;
        var shown = empty && !focused ? Placeholder.AsSpan() : _text.AsSpan();
        var cursor = dot * 2;
        var room = PixelSize.X - cursor - dot;

        // A long entry keeps its end in view.
        while (shown.Length > 0 && face.Font.Measure(shown) * dot > room)
        {
            shown = shown[(shown.Length > 1 && char.IsHighSurrogate(shown[0]) ? 2 : 1)..];
        }

        var width = shown.IsEmpty ? 0 : face.Font.Measure(shown) * dot;
        var top = (PixelSize.Y - face.Line * dot) / 2;
        var left = Readout ? PixelSize.X - cursor - dot - width : 0;
        if (!shown.IsEmpty)
            handle.DrawString(face.Font.Font, new Vector2(left, top), shown, face.DrawScale, empty ? PlaceholderColor : TextColor);

        if (focused && _blink < BlinkTime)
        {
            var capTop = top + face.Font.CapTop * dot;
            handle.DrawRect(UIBox2.FromDimensions(left + width, capTop, cursor, face.Font.CapHeight * dot), TextColor);
        }
    }
}
