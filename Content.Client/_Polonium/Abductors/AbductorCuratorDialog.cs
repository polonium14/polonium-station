using System.Numerics;
using Content.Client._Polonium.UserInterface.PixelUi;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Abductors;

/// <summary>
/// The Curator's window: a framed silhouette and text typed out letter by letter.
/// It never takes keyboard focus, so the character stays under the player's control.
/// </summary>
public sealed class AbductorCuratorDialog : Control
{
    private const string BodyPath = "/Textures/_Polonium/Interface/Abductors/curator_dialog.png";
    private const string PortraitPath = "/Textures/_Polonium/Interface/Abductors/curator_portrait.png";
    private const string TextFontPath = "/Fonts/_Polonium/DepartureMono/DepartureMono-Regular.otf";

    // Departure Mono is drawn on an 11 pixel em, which is size 33 at a quarter of the engine's scale.
    private const int TextFontSize = 33;
    private const float TextFontScale = 0.25f;

    private const int ArtWidth = 256;
    private const int ArtHeight = 56;
    private const int PortraitLeft = 6;
    private const int PortraitTop = 6;
    private const int TextLeft = 58;
    private const int TextWidth = 186;
    private const int TitleTop = 8;
    private const int TextTop = 18;
    private const int TextInset = 2;
    private const int LineStep = 14;
    private const int MarkerRight = 250;
    private const int MarkerTop = 48;

    private const float LetterDelay = 0.05f;
    private const float CommaDelay = 0.16f;
    private const float StopDelay = 0.32f;
    private const float HoldTime = 3.5f;
    private const float HoldPerLetter = 0.03f;

    private static readonly Color TitleColor = Color.FromHex("#ff0099");
    private static readonly Color TextColor = Color.FromHex("#ece6ea");

    private readonly Texture _body;
    private readonly Texture _portrait;
    private readonly PixelFont _titleFont;
    private readonly PixelFont _textFont;
    private readonly string _title;
    private readonly List<(int Start, int Length)> _lines = new();

    private string _text = string.Empty;
    private int _wrappedScale;
    private int _shown;
    private float _wait;
    private float _hold;

    /// <summary>
    /// Raised on a frame in which at least one letter appeared.
    /// </summary>
    public event Action? LetterShown;

    /// <summary>
    /// Raised when the line has been read or dismissed.
    /// </summary>
    public event Action? Finished;

    public AbductorCuratorDialog(IResourceCache cache)
    {
        MouseFilter = MouseFilterMode.Stop;
        _body = cache.GetTexture(BodyPath);
        _portrait = cache.GetTexture(PortraitPath);
        _titleFont = PixelFont.Tiny5(cache);
        _textFont = new PixelFont(cache.GetFont(TextFontPath, TextFontSize), TextFontScale, 3, 8);
        _title = Loc.GetString("abductor-curator-title");
    }

    public void Start(string text, bool instant)
    {
        _text = text;
        _shown = instant ? text.Length : 0;
        _wait = 0f;
        _hold = HoldTime + text.Length * HoldPerLetter;
        Wrap();
    }

    // The text's own pixels stay whole on screen, so its size follows the interface scale in steps.
    private static int TextDot(int scale)
    {
        return (scale + 1) / 2;
    }

    private void Wrap()
    {
        _wrappedScale = PixelCanvas.GetScale(UIScale);
        var dot = TextDot(_wrappedScale);
        var width = TextWidth * _wrappedScale;

        _lines.Clear();
        var start = 0;
        while (start < _text.Length)
        {
            var rest = _text.AsSpan(start);
            var fit = _textFont.Fit(rest, width, dot);
            if (fit < rest.Length && rest[fit] != ' ')
            {
                var space = _text.LastIndexOf(' ', start + fit - 1, fit);
                if (space > start)
                    fit = space - start;
            }

            fit = Math.Max(1, fit);
            _lines.Add((start, fit));
            start += fit;
            while (start < _text.Length && _text[start] == ' ')
                start++;
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (PixelCanvas.GetScale(UIScale) != _wrappedScale)
            Wrap();

        if (_shown >= _text.Length)
        {
            _hold -= args.DeltaSeconds;
            if (_hold <= 0f)
                Finished?.Invoke();
            return;
        }

        _wait -= args.DeltaSeconds;
        var spoke = false;
        while (_wait <= 0f && _shown < _text.Length)
        {
            var character = _text[_shown++];
            _wait += Delay(character);
            spoke |= char.IsLetterOrDigit(character);
        }

        if (spoke)
            LetterShown?.Invoke();
    }

    private static float Delay(char character)
    {
        return character switch
        {
            '.' or '!' or '?' => StopDelay,
            ',' or ':' or ';' => CommaDelay,
            _ => LetterDelay,
        };
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        args.Handle();
        if (_shown < _text.Length)
            _shown = _text.Length;
        else
            Finished?.Invoke();
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        return new Vector2(ArtWidth, ArtHeight) * PixelCanvas.GetScale(UIScale) / UIScale;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var canvas = new PixelCanvas(handle, UIScale);
        canvas.Texture(_body, 0, 0);
        canvas.Texture(_portrait, PortraitLeft, PortraitTop);
        canvas.Text(_titleFont, TextLeft, TitleTop, _title, TitleColor);

        var dot = TextDot(canvas.Scale);
        var left = TextLeft * canvas.Scale;
        var top = TextTop * canvas.Scale + TextInset * dot;
        for (var i = 0; i < _lines.Count; i++)
        {
            var (start, length) = _lines[i];
            var visible = Math.Min(length, _shown - start);
            if (visible <= 0)
                break;

            var position = new Vector2(left, top + i * LineStep * dot);
            handle.DrawString(_textFont.Font, position, _text.AsSpan(start, visible), _textFont.Scale * dot, TextColor);
        }

        if (_shown < _text.Length)
            return;

        canvas.Rect(MarkerRight - 5, MarkerTop, 5, 1, TitleColor);
        canvas.Rect(MarkerRight - 4, MarkerTop + 1, 3, 1, TitleColor);
        canvas.Rect(MarkerRight - 3, MarkerTop + 2, 1, 1, TitleColor);
    }
}
