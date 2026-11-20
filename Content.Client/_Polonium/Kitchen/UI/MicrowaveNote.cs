using Content.Client._Polonium.UserInterface.PixelUi;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Kitchen.UI;

/// <param name="Uid">Invalid when the thing only exists as a picture in the chamber.</param>
public readonly record struct MicrowaveRow(EntityUid Uid, string Name);

public sealed partial class MicrowaveNote : Control
{
    [Dependency] private IResourceCache _cache = default!;

    public const int ArtWidth = 96;
    public const int ArtHeight = 124;

    private const int MaxRows = 10;
    private const int TextLeft = 5;
    private const int TextRight = 7;
    private const int HeaderTop = 13;
    private const int UnderlineTop = 23;
    private const int RowsTop = 23;
    private const int RowHeight = 9;
    private const int StrikeOffset = 5;
    private const float RewriteStep = 0.05f;
    private const string Ellipsis = "…";

    private static readonly Color Ink = Color.FromHex("#2f3f6b");
    private static readonly Color FadedInk = Color.FromHex("#8c93a8");

    private readonly Texture _paper;
    private readonly PixelFont _font;
    private readonly string _header;
    private readonly string _empty;
    private readonly List<Row> _rows = new();
    private string _count = string.Empty;
    private int _capacity = -1;
    private int _mouseRow = -1;

    public bool Interactive;
    public bool ReducedMotion;
    public EntityUid? Highlighted;

    public event Action<EntityUid>? OnItemPressed;
    public event Action<EntityUid?>? OnHoverChanged;

    public MicrowaveNote()
    {
        IoCManager.InjectDependencies(this);

        _paper = _cache.GetTexture(MicrowaveDeviceWindow.TexturePath + "note.png");
        _font = PixelFont.Tiny5(_cache);
        _header = Loc.GetString("polonium-microwave-note-header");
        _empty = Loc.GetString("polonium-microwave-note-empty");

        MouseFilter = MouseFilterMode.Stop;
    }

    /// <summary>
    /// Shows the given contents on the note, one row each, with the count against the capacity in the header.
    /// </summary>
    public void SetRows(IReadOnlyList<MicrowaveRow> rows, int capacity)
    {
        if (capacity != _capacity || rows.Count != _rows.Count)
        {
            _capacity = capacity;
            _count = $"{rows.Count}/{capacity}";
        }

        var listChanged = rows.Count != _rows.Count;
        for (var i = 0; i < rows.Count; i++)
        {
            var (uid, name) = rows[i];
            if (i == _rows.Count)
                _rows.Add(new Row());

            var row = _rows[i];
            if (row.Uid == uid && row.Name == name)
                continue;

            // The same thing under a new name is rewritten letter by letter, as the game renamed it in front of the player.
            var renamed = row.Uid == uid && uid.IsValid() && !ReducedMotion;
            listChanged |= row.Uid != uid;
            row.Uid = uid;
            row.Former = renamed ? row.Name : string.Empty;
            row.Name = name;
            row.Rewritten = 0f;
            Write(row);
        }

        if (_rows.Count > rows.Count)
            _rows.RemoveRange(rows.Count, _rows.Count - rows.Count);

        if (!listChanged || _mouseRow < 0)
            return;

        var hovered = _mouseRow;
        _mouseRow = -2;
        SetMouseRow(hovered);
    }

    private void Write(Row row)
    {
        var name = row.Name;
        var done = (int) (row.Rewritten / RewriteStep);
        if (done < row.Former.Length)
            name = string.Concat(name.AsSpan(0, Math.Min(done, name.Length)), row.Former.AsSpan(done));
        else
            row.Former = string.Empty;

        var maxWidth = ArtWidth - TextLeft - TextRight;
        var shown = _font.Measure(name) <= maxWidth
            ? name
            : string.Concat(name.AsSpan(0, _font.Fit(name, maxWidth - _font.Measure(Ellipsis))).TrimEnd(), Ellipsis);

        row.Shown = shown;
        row.Width = _font.Measure(shown) - 1;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        foreach (var row in _rows)
        {
            if (row.Former.Length == 0)
                continue;

            if (ReducedMotion)
            {
                row.Former = string.Empty;
                Write(row);
                continue;
            }

            var done = (int) (row.Rewritten / RewriteStep);
            row.Rewritten += args.DeltaSeconds;
            if ((int) (row.Rewritten / RewriteStep) != done)
                Write(row);
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var canvas = new PixelCanvas(handle, UIScale);
        var ink = Interactive ? Ink : FadedInk;
        canvas.Texture(_paper, 0, 0);
        canvas.Text(_font, TextLeft, HeaderTop, _header, ink);
        canvas.Text(_font, ArtWidth - TextRight - _font.Measure(_count) + 1, HeaderTop, _count, ink);
        canvas.Rect(TextLeft - 1, UnderlineTop, ArtWidth - TextLeft - TextRight + 2, 1, ink);

        if (_rows.Count == 0)
        {
            canvas.Text(_font, TextLeft, RowsTop + RowHeight / 2, _empty, FadedInk);
            return;
        }

        for (var i = 0; i < _rows.Count && i < MaxRows; i++)
        {
            var row = _rows[i];
            var top = RowsTop + i * RowHeight;
            canvas.Text(_font, TextLeft, top, row.Shown, ink);
            if (Interactive && row.Uid.IsValid() && row.Uid == Highlighted)
                canvas.Rect(TextLeft - 1, top + StrikeOffset, row.Width + 2, 1, ink);
        }
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        SetMouseRow(RowAt(args.RelativePixelPosition.Y));
    }

    protected override void MouseExited()
    {
        base.MouseExited();

        SetMouseRow(-1);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick || !Interactive)
            return;

        SetMouseRow(RowAt(args.RelativePixelPosition.Y));
        if (_mouseRow < 0)
            return;

        OnItemPressed?.Invoke(_rows[_mouseRow].Uid);
        args.Handle();
    }

    private int RowAt(float pixelY)
    {
        var y = (int) (pixelY / PixelCanvas.GetScale(UIScale)) - RowsTop - 1;
        return y < 0 ? -1 : y / RowHeight;
    }

    private void SetMouseRow(int row)
    {
        if (row >= _rows.Count || row >= MaxRows || row >= 0 && !_rows[row].Uid.IsValid())
            row = -1;

        DefaultCursorShape = row >= 0 && Interactive ? CursorShape.Hand : CursorShape.Arrow;
        if (row == _mouseRow)
            return;

        _mouseRow = row;
        OnHoverChanged?.Invoke(row < 0 ? null : _rows[row].Uid);
    }

    private sealed class Row
    {
        public EntityUid Uid;
        public string Name = string.Empty;
        public string Former = string.Empty;
        public string Shown = string.Empty;
        public float Rewritten;
        public int Width;
    }
}
