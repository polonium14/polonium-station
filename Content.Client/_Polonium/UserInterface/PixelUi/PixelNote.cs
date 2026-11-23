using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.UserInterface.PixelUi;

/// <param name="Key">The thing the row stands for. A row with an invalid key cannot be pointed at or clicked.</param>
/// <param name="Swatch">Colour of a small sample drawn before the text.</param>
public readonly record struct PixelNoteRow(EntityUid Key, string Text, Color? Swatch = null);

/// <summary>
/// One list on a <see cref="PixelNote"/>: a heading with a tally on the right, a rule under it and the rows.
/// </summary>
public sealed class PixelNoteSection
{
    internal readonly List<PixelNote.Line> Lines = new();
    internal int Scroll;
    internal string HeadingShown = string.Empty;
    internal string? FittedHeading;
    internal string? FittedTally;

    /// <summary>
    /// Top of the heading line on the paper, in art pixels.
    /// </summary>
    public int Top;

    public int MaxRows = 10;
    public string Heading = string.Empty;

    /// <summary>
    /// Text at the right end of the heading line, such as "3/6".
    /// </summary>
    public string Tally = string.Empty;

    /// <summary>
    /// Written in place of the rows when there are none.
    /// </summary>
    public string Empty = string.Empty;

    /// <summary>
    /// How far below the first row the <see cref="Empty"/> text stands, in art pixels.
    /// </summary>
    public int EmptyDrop;

    /// <summary>
    /// Writes the heading in the faded ink, for a section that has nothing to list.
    /// </summary>
    public bool Dimmed;

    /// <summary>
    /// Written in the last row when more rows follow below. Leaving it empty cuts a long list off without scrolling.
    /// </summary>
    public string More = string.Empty;

    /// <summary>
    /// Written in the last row when a long list has been scrolled to its end.
    /// </summary>
    public string End = string.Empty;

    internal bool Scrolls => More.Length > 0 && Lines.Count > MaxRows;
    internal int Shown => Scrolls ? MaxRows - 1 : Math.Min(Lines.Count, MaxRows);
}

/// <summary>
/// A paper note that lists what a device holds. Rows that stand for a thing can be clicked, and a row whose thing
/// got a new name is rewritten letter by letter.
/// </summary>
public sealed class PixelNote : Control
{
    private const int TextLeft = 5;
    private const int Margins = 12;
    private const int RuleDrop = 10;
    private const int RowHeight = 9;
    private const int Baseline = 7;
    private const int StrikeOffset = 5;
    private const int SwatchWidth = 7;
    private const int TallyGap = 3;
    private const float RewriteStep = 0.05f;
    private const string Ellipsis = "…";

    private static readonly Color Ink = Color.FromHex("#2f3f6b");
    private static readonly Color FadedInk = Color.FromHex("#8c93a8");
    private static readonly Color SwatchFrame = Color.FromHex("#171717");

    private readonly Texture _paper;
    private readonly PixelFont _font;
    private readonly int _paperWidth;
    private readonly List<PixelNoteSection> _sections = new();
    private PixelNoteSection? _mouseSection;
    private EntityUid? _mouseKey;
    private int _mouseRow = -1;
    private float? _mouseY;
    private int _fitScale;

    public bool Interactive;
    public bool ReducedMotion;
    public EntityUid? Highlighted;

    public event Action<EntityUid>? OnItemPressed;
    public event Action<EntityUid?>? OnHoverChanged;

    /// <param name="paper">Picture of the empty note. The control is as large as this picture.</param>
    /// <param name="paperWidth">Width of the sheet itself in art pixels, without anything the picture adds around it.</param>
    public PixelNote(Texture paper, PixelFont font, int paperWidth)
    {
        _paper = paper;
        _font = font;
        _paperWidth = paperWidth;

        MouseFilter = MouseFilterMode.Stop;
    }

    /// <summary>
    /// Adds a list to the note.
    /// </summary>
    /// <param name="top">Top of its heading line on the picture, in art pixels.</param>
    public PixelNoteSection AddSection(int top, int maxRows)
    {
        var section = new PixelNoteSection { Top = top, MaxRows = maxRows };
        _sections.Add(section);
        return section;
    }

    /// <summary>
    /// Shows the given rows in a section of the note.
    /// </summary>
    public void SetRows(PixelNoteSection section, IReadOnlyList<PixelNoteRow> rows)
    {
        Refit();
        if (!ReferenceEquals(section.Heading, section.FittedHeading) || !ReferenceEquals(section.Tally, section.FittedTally))
            FitHeading(section);

        var lines = section.Lines;
        var listChanged = rows.Count != lines.Count;
        for (var i = 0; i < rows.Count; i++)
        {
            var (key, text, swatch) = rows[i];
            if (i == lines.Count)
                lines.Add(new Line());

            var line = lines[i];
            if (line.Key == key && line.Text == text && line.Swatch == swatch)
                continue;

            // The same thing under a new name is rewritten letter by letter, as the game renamed it in front of the player.
            var renamed = line.Key == key && key.IsValid() && !ReducedMotion;
            listChanged |= line.Key != key;
            line.Key = key;
            line.Former = renamed ? line.Text : string.Empty;
            line.Text = text;
            line.Swatch = swatch;
            line.Rewritten = 0f;
            Write(line);
        }

        if (lines.Count > rows.Count)
            lines.RemoveRange(rows.Count, lines.Count - rows.Count);

        section.Scroll = Math.Clamp(section.Scroll, 0, Math.Max(0, lines.Count - section.Shown));
        if (listChanged)
            RefreshHover();
    }

    // The text is cut to the paper in screen pixels, so a new interface scale means cutting it again.
    private void Refit()
    {
        var scale = PixelCanvas.GetScale(UIScale);
        if (scale == _fitScale)
            return;

        _fitScale = scale;
        foreach (var section in _sections)
        {
            FitHeading(section);
            foreach (var line in section.Lines)
            {
                Write(line);
            }
        }
    }

    private void FitHeading(PixelNoteSection section)
    {
        section.FittedHeading = section.Heading;
        section.FittedTally = section.Tally;
        var room = (_paperWidth - Margins - TallyGap) * _fitScale - _font.Measure(section.Tally, _fitScale);
        section.HeadingShown = Fit(section.Heading, room);
    }

    private void Write(Line line)
    {
        var text = line.Text;
        var done = (int) (line.Rewritten / RewriteStep);
        if (done < line.Former.Length)
            text = string.Concat(text.AsSpan(0, Math.Min(done, text.Length)), line.Former.AsSpan(done));
        else
            line.Former = string.Empty;

        line.Shown = Fit(text, (_paperWidth - Margins - (line.Swatch == null ? 0 : SwatchWidth)) * _fitScale);
        line.Width = _font.Measure(line.Shown, _fitScale) - _font.Trail(_fitScale);
    }

    private string Fit(string text, int maxWidth)
    {
        if (_font.Measure(text, _fitScale) <= maxWidth)
            return text;

        var kept = _font.Fit(text, maxWidth - _font.Measure(Ellipsis, _fitScale), _fitScale);
        return string.Concat(text.AsSpan(0, kept).TrimEnd(), Ellipsis);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        Refit();
        foreach (var section in _sections)
        {
            foreach (var line in section.Lines)
            {
                if (line.Former.Length == 0)
                    continue;

                if (ReducedMotion)
                {
                    line.Former = string.Empty;
                    Write(line);
                    continue;
                }

                var done = (int) (line.Rewritten / RewriteStep);
                line.Rewritten += args.DeltaSeconds;
                if ((int) (line.Rewritten / RewriteStep) != done)
                    Write(line);
            }
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var canvas = new PixelCanvas(handle, UIScale);
        var unit = canvas.Scale;
        var ink = Interactive ? Ink : FadedInk;
        var left = TextLeft;
        var right = _paperWidth - Margins + TextLeft;
        canvas.Texture(_paper, 0, 0);
        foreach (var section in _sections)
        {
            var tallyLeft = right * unit - _font.Measure(section.Tally, unit) + _font.Trail(unit);
            Text(handle, unit, left * unit, section.Top, section.HeadingShown, section.Dimmed ? FadedInk : ink);
            Text(handle, unit, tallyLeft, section.Top, section.Tally, ink);
            canvas.Rect(left - 1, section.Top + RuleDrop, right - left + 2, 1, ink);

            var rows = section.Top + RuleDrop;
            var lines = section.Lines;
            if (lines.Count == 0)
            {
                Text(handle, unit, left * unit, rows + section.EmptyDrop, section.Empty, FadedInk);
                continue;
            }

            var shown = section.Shown;
            for (var i = 0; i < shown; i++)
            {
                var line = lines[i + section.Scroll];
                var top = rows + i * RowHeight;
                var textLeft = left;
                if (line.Swatch is { } swatch)
                {
                    canvas.Rect(left, top + 2, 4, 5, SwatchFrame);
                    canvas.Rect(left + 1, top + 3, 2, 3, swatch);
                    textLeft += SwatchWidth;
                }

                Text(handle, unit, textLeft * unit, top, line.Shown, ink);
                if (Interactive && line.Key.IsValid() && line.Key == Highlighted)
                    handle.DrawRect(UIBox2.FromDimensions((textLeft - 1) * unit, (top + StrikeOffset) * unit, line.Width + 2 * unit, unit), ink);
            }

            if (section.Scrolls)
                Text(handle, unit, left * unit, rows + shown * RowHeight, section.Scroll + shown < lines.Count ? section.More : section.End, FadedInk);
        }
    }

    // Writes a line that stands on the same row of the paper whatever the height of the font is.
    private void Text(DrawingHandleScreen handle, int unit, int left, int top, string text, Color color)
    {
        var scale = _font.Scale * unit;
        var position = new Vector2(left, (top + Baseline) * unit - _font.Font.GetAscent(scale));
        handle.DrawString(_font.Font, position, text, scale, color);
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        _mouseY = args.RelativePixelPosition.Y;
        RefreshHover();
    }

    protected override void MouseExited()
    {
        base.MouseExited();

        _mouseY = null;
        RefreshHover();
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick || !Interactive)
            return;

        _mouseY = args.RelativePixelPosition.Y;
        RefreshHover();
        if (_mouseSection == null)
            return;

        OnItemPressed?.Invoke(_mouseSection.Lines[_mouseRow].Key);
        args.Handle();
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);

        foreach (var section in _sections)
        {
            if (!section.Scrolls || args.Delta.Y == 0f)
                continue;

            section.Scroll = Math.Clamp(section.Scroll + (args.Delta.Y < 0f ? 1 : -1), 0, section.Lines.Count - section.Shown);
            args.Handle();
        }

        RefreshHover();
    }

    private void RefreshHover()
    {
        PixelNoteSection? found = null;
        var row = -1;
        if (_mouseY is { } pixelY)
        {
            var y = (int) (pixelY / PixelCanvas.GetScale(UIScale));
            foreach (var section in _sections)
            {
                var offset = y - section.Top - RuleDrop - 1;
                var index = offset < 0 ? -1 : offset / RowHeight;
                if (index < 0 || index >= section.Shown || !section.Lines[index + section.Scroll].Key.IsValid())
                    continue;

                found = section;
                row = index + section.Scroll;
                break;
            }
        }

        DefaultCursorShape = found != null && Interactive ? CursorShape.Hand : CursorShape.Arrow;
        var key = found?.Lines[row].Key;
        _mouseSection = found;
        _mouseRow = row;
        if (key == _mouseKey)
            return;

        _mouseKey = key;
        OnHoverChanged?.Invoke(key);
    }

    internal sealed class Line
    {
        public EntityUid Key;
        public string Text = string.Empty;
        public string Former = string.Empty;
        public string Shown = string.Empty;
        public Color? Swatch;
        public float Rewritten;
        public int Width;
    }
}
