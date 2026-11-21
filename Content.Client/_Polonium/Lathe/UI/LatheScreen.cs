using System.Numerics;
using Content.Client._Polonium.UserInterface.PixelUi;
using Content.Shared.Lathe.Prototypes;
using Content.Shared.Research.Prototypes;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._Polonium.Lathe.UI;

/// <summary>
/// The green screen of the console: the catalog on the left, the plotter on the right, soft key captions along the bottom.
/// </summary>
public sealed class LatheScreen : Control
{
    public const int ArtWidth = 280;
    public const int ArtHeight = 133;
    public const int SoftSlots = 4;
    public const int PlotTop = HeaderHeight;

    private const int PaneWidth = 164;
    private const int HeaderHeight = 11;
    private const int PromptHeight = 12;
    private const int ListTop = HeaderHeight + PromptHeight;
    private const int SoftTop = 122;
    private const int SoftHeight = 11;
    private const int ArrowCell = 22;
    private const int SoftCell = 59;
    private const int Padding = 3;
    private const int ReadoutTop = HeaderHeight + LathePlot.ArtHeight + 1;
    private const int Line = LathePalette.ScreenRow;
    private const int BarTop = ReadoutTop + Line * 2;
    private const int BarHeight = 3;
    private const int BarPitch = 5;
    private const int BarSegments = 22;
    private const int GlitchWidth = 4;
    private const int GlitchHeight = 5;
    private const int CatalogGlitchSalt = 3;
    private const int PlotGlitchSalt = 4;

    private readonly LatheConsoleModel _model;
    private readonly LatheScreenFont _font;
    private readonly Texture _iconTexture;
    private readonly LatheCatalog _catalog;
    private readonly LathePlot _plot;
    private readonly LatheTextField _search;
    private readonly LatheScreenOverlay _overlay;
    private readonly string _catalogTitle;
    private readonly string _plotTitle;
    private readonly string _prompt;
    private readonly string _allCategories;
    private readonly string _idle;
    private readonly string _preview;
    private readonly string _shortage;
    private readonly string _stopped;
    private readonly char[] _head = "X 000 Y 000".ToCharArray();
    private readonly string[] _softText = new string[SoftSlots];
    private readonly LatheCategoryPrototype?[] _softCategory = new LatheCategoryPrototype?[SoftSlots];

    private string _count = string.Empty;
    private int _shownCount = -1;
    private string _name = string.Empty;
    private LatheRecipePrototype? _named;
    private string _left = string.Empty;
    private int _shownTenths = -1;
    private int _softOffset;

    /// <summary>
    /// The batch the item being printed belongs to, as worked out by the ticket rack.
    /// </summary>
    public string BatchText = string.Empty;

    public LatheCatalog Catalog => _catalog;

    public LatheScreen(LatheConsoleModel model, LatheIconCache icons, LatheScreenFont font, Texture iconTexture)
    {
        _model = model;
        _font = font;
        _iconTexture = iconTexture;
        _catalogTitle = Loc.GetString("polonium-lathe-catalog-title").ToUpperInvariant();
        _plotTitle = Loc.GetString("polonium-lathe-plot-title").ToUpperInvariant();
        _prompt = Loc.GetString("polonium-lathe-search-prompt").ToUpperInvariant();
        _allCategories = Loc.GetString("lathe-menu-category-all").ToUpperInvariant();
        _idle = Loc.GetString("polonium-lathe-idle").ToUpperInvariant();
        _preview = Loc.GetString("polonium-lathe-preview");
        _shortage = Loc.GetString("polonium-lathe-shortage").ToUpperInvariant();
        _stopped = Loc.GetString("polonium-lathe-stopped").ToUpperInvariant();

        _search = new LatheTextField
        {
            Font = font,
            Placeholder = Loc.GetString("lathe-menu-search-designs").ToLowerInvariant(),
        };
        _search.OnTextChanged += _model.SetSearch;
        AddChild(_search);

        _catalog = new LatheCatalog(model, icons, font);
        AddChild(_catalog);

        _plot = new LathePlot(model, icons);
        AddChild(_plot);

        _overlay = new LatheScreenOverlay(model);
        AddChild(_overlay);

        _model.CatalogChanged += RefreshSoft;
        RefreshSoft();
    }

    /// <summary>
    /// The outer soft keys page through the categories, the four in the middle pick one.
    /// </summary>
    public void PressSoft(int key)
    {
        var last = Math.Max(0, _model.Categories.Count - SoftSlots);
        if (key == 0)
            _softOffset = Math.Max(0, _softOffset - 1);
        else if (key == SoftSlots + 1)
            _softOffset = Math.Min(last, _softOffset + 1);
        else if (_softOffset + key - 1 < _model.Categories.Count)
            _model.SetCategory(_model.Categories[_softOffset + key - 1]);

        RefreshSoft();
    }

    private void RefreshSoft()
    {
        _softOffset = Math.Clamp(_softOffset, 0, Math.Max(0, _model.Categories.Count - SoftSlots));
        for (var i = 0; i < SoftSlots; i++)
        {
            var index = _softOffset + i;
            if (index >= _model.Categories.Count)
            {
                _softText[i] = string.Empty;
                _softCategory[i] = null;
                continue;
            }

            _softCategory[i] = _model.Categories[index];
            _softText[i] = _softCategory[i] is { } category ? Loc.GetString(category.Name).ToUpperInvariant() : _allCategories;
        }
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var scale = PixelCanvas.GetScale(UIScale);
        var face = _font.Face(scale);
        var searchLeft = Padding * 2 + face.Font.Measure(_prompt) * face.Dot / (float) scale;
        _search.Arrange(LatheLayout.Box(UIScale, searchLeft, HeaderHeight, PaneWidth - Padding - searchLeft, PromptHeight));
        _catalog.Arrange(LatheLayout.Box(UIScale, 0, ListTop, PaneWidth - 1, SoftTop - ListTop));
        _plot.Arrange(LatheLayout.Box(UIScale, PaneWidth, HeaderHeight, LathePlot.ArtWidth, LathePlot.ArtHeight));
        _overlay.Arrange(UIBox2.FromDimensions(Vector2.Zero, finalSize));
        return finalSize;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var canvas = new LatheScreenCanvas(handle, _font, UIScale);
        DrawCatalogHeader(canvas);
        canvas.Text(Padding, HeaderHeight, PromptHeight, PaneWidth, _prompt, LathePalette.Screen);
        canvas.RuleAbove(0, ListTop, PaneWidth, LathePalette.ScreenDim);
        canvas.RuleLeftOf(PaneWidth, 0, SoftTop, LathePalette.ScreenDim);

        canvas.Rect(PaneWidth, 0, ArtWidth - PaneWidth, HeaderHeight, LathePalette.Screen);
        DrawTitle(canvas, PaneWidth + Padding, _plotTitle, PlotGlitchSalt);
        Digits(_head, 2, _plot.Head.X);
        Digits(_head, 8, _plot.Head.Y);
        canvas.TextRight(ArtWidth - Padding, 0, HeaderHeight, _head, LathePalette.ScreenBack);

        DrawReadout(canvas);
        DrawSoft(canvas);
    }

    private void DrawCatalogHeader(LatheScreenCanvas canvas)
    {
        canvas.Rect(0, 0, PaneWidth, HeaderHeight, LathePalette.Screen);
        if (_model.Catalog.Count != _shownCount)
        {
            _shownCount = _model.Catalog.Count;
            _count = Loc.GetString("lathe-menu-recipe-count", ("count", _shownCount)).ToUpperInvariant();
        }

        canvas.TextRight(PaneWidth - Padding, 0, HeaderHeight, _count, LathePalette.ScreenBack);
        DrawTitle(canvas, Padding, _catalogTitle, CatalogGlitchSalt);
    }

    /// <summary>
    /// A header title. On an emagged lathe one of its letters is garbage.
    /// </summary>
    private void DrawTitle(LatheScreenCanvas canvas, int left, string title, int salt)
    {
        if (!_model.Emagged || title.Length < 3)
        {
            canvas.Text(left, 0, HeaderHeight, ArtWidth, title, LathePalette.ScreenBack);
            return;
        }

        var cut = _model.Glitch(salt, title.Length);
        var head = title.AsSpan(0, cut);
        canvas.Text(left, 0, HeaderHeight, ArtWidth, head, LathePalette.ScreenBack);
        var x = left + (canvas.Width(head) + canvas.Dot * 2 + canvas.Scale - 1) / canvas.Scale;
        var y = (HeaderHeight - GlitchHeight) / 2;
        for (var i = 0; i < GlitchWidth * GlitchHeight; i++)
        {
            if ((i % GlitchWidth + i / GlitchWidth) % 2 == 0)
                canvas.Rect(x + i % GlitchWidth, y + i / GlitchWidth, 1, 1, LathePalette.ScreenBack);
        }

        canvas.Text(x + GlitchWidth + 1, 0, HeaderHeight, ArtWidth, title.AsSpan(cut + 1), LathePalette.ScreenBack);
    }

    private void DrawReadout(LatheScreenCanvas canvas)
    {
        var left = PaneWidth + Padding;
        var width = ArtWidth - PaneWidth - Padding * 2;
        if (_model.Denying)
        {
            canvas.Rect(left - 2, ReadoutTop, width + 4, Line, LathePalette.Screen);
            canvas.Text(left, ReadoutTop, Line, width, _shortage, LathePalette.ScreenBack);
            canvas.Text(left, ReadoutTop + Line, Line, width, _model.DeniedText, LathePalette.ScreenMid);
            DrawBar(canvas, left, _model.Printing == null ? 0 : (int) MathF.Round(_model.Progress * BarSegments));
            return;
        }

        if (_model.Printing is { } printing)
        {
            SetName(printing);
            var tenths = (int) Math.Ceiling(_model.SecondsLeft * 10d);
            if (tenths != _shownTenths)
            {
                _shownTenths = tenths;
                _left = Loc.GetString("polonium-lathe-seconds", ("seconds", tenths / 10f));
            }

            canvas.Text(left, ReadoutTop, Line, width, _name, LathePalette.ScreenHot);
            canvas.Text(left, ReadoutTop + Line, Line, width, BatchText, LathePalette.ScreenMid);
            canvas.TextRight(left + width, ReadoutTop + Line, Line, _left, LathePalette.ScreenMid);
            DrawBar(canvas, left, (int) MathF.Round(_model.Progress * BarSegments));
            return;
        }

        // The stop notice keeps the first line until the next job, a pointed-at recipe then only gets the second.
        var stopped = _model.Stopped && _model.Queue.Length == 0;
        if (stopped)
        {
            canvas.Rect(left - 2, ReadoutTop, width + 4, Line, LathePalette.Screen);
            canvas.Text(left, ReadoutTop, Line, width, _stopped, LathePalette.ScreenBack);
        }

        if (_model.Hover is { } hover)
        {
            SetName(hover);
            var tenths = (int) Math.Round(_model.PrintTime(hover).TotalSeconds * 10d);
            if (tenths != _shownTenths)
            {
                _shownTenths = tenths;
                _left = Loc.GetString("polonium-lathe-seconds", ("seconds", tenths / 10f));
            }

            if (!stopped)
                canvas.Text(left, ReadoutTop, Line, width, _name, LathePalette.ScreenHot);

            canvas.Text(left, ReadoutTop + Line, Line, width, _preview, LathePalette.ScreenMid);
            canvas.TextRight(left + width, ReadoutTop + Line, Line, _left, LathePalette.ScreenMid);
        }
        else if (!stopped)
        {
            canvas.Text(left, ReadoutTop, Line, width, _idle, LathePalette.ScreenHot);
        }

        DrawBar(canvas, left, 0);
    }

    private void SetName(LatheRecipePrototype recipe)
    {
        if (_named == recipe)
            return;

        _named = recipe;
        _shownTenths = -1;
        _name = _model.RecipeName(recipe).ToUpperInvariant();
    }

    private static void DrawBar(LatheScreenCanvas canvas, int left, int lit)
    {
        for (var i = 0; i < BarSegments; i++)
        {
            canvas.Rect(left + i * BarPitch, BarTop, BarPitch - 1, BarHeight, i < lit ? LathePalette.Screen : LathePalette.ScreenDim);
        }
    }

    private void DrawSoft(LatheScreenCanvas canvas)
    {
        canvas.RuleBelow(0, SoftTop, ArtWidth, LathePalette.ScreenDim);
        var arrow = LathePalette.IconCell;
        var arrowY = SoftTop + (SoftHeight - arrow) / 2;
        canvas.Texture(_iconTexture, LathePalette.Icon(LatheKeyIcon.Left), (ArrowCell - arrow) / 2, arrowY, LathePalette.Screen);
        canvas.Texture(_iconTexture, LathePalette.Icon(LatheKeyIcon.Right), ArtWidth - ArrowCell + (ArrowCell - arrow) / 2, arrowY, LathePalette.Screen);
        canvas.RuleLeftOf(ArrowCell, SoftTop, SoftHeight, LathePalette.ScreenDim);

        for (var i = 0; i < SoftSlots; i++)
        {
            var x = ArrowCell + i * SoftCell;
            var selected = _softText[i].Length > 0 && _softCategory[i] == _model.Category;
            if (selected)
                canvas.Rect(x, SoftTop, SoftCell, SoftHeight, LathePalette.Screen);

            canvas.TextCentered(x + 1, SoftTop, SoftHeight, SoftCell - 2, _softText[i], selected ? LathePalette.ScreenBack : LathePalette.Screen);
            canvas.RuleLeftOf(x + SoftCell, SoftTop, SoftHeight, LathePalette.ScreenDim);
        }
    }

    private static void Digits(char[] text, int start, int value)
    {
        value = Math.Clamp(value, 0, 999);
        text[start] = (char) ('0' + value / 100);
        text[start + 1] = (char) ('0' + value / 10 % 10);
        text[start + 2] = (char) ('0' + value % 10);
    }
}

/// <summary>
/// Lies over the whole screen: the scanline raster and, on an emagged lathe, a few faults that hop to a new
/// place every few seconds. Nothing in it blinks or changes brightness.
/// </summary>
public sealed class LatheScreenOverlay : Control
{
    /// <summary>
    /// The plotter draws this many rows above the torn strip shifted to the side.
    /// </summary>
    public const int SliceHeight = 5;

    private const int LinePeriod = 3;
    private const int TearFirst = 18;
    private const int TearRows = 94;
    private const int TearHeight = 2;
    private const int TearSalt = 1;
    private const int DashSalt = 100;
    private const int DashMin = 2;
    private const int DashSpread = 9;
    private const int CellWidth = 5;
    private const int CellHeight = 7;
    private const int CellGrid = 8;
    private const int CellColumns = 34;
    private const int CellRows = 13;
    private const int CellTop = 14;
    private const int CellSalt = 10;
    private const int Cells = 2;
    private const int StuckX = 146;
    private const int StuckY = 76;

    private static readonly Color Raster = Color.Black.WithAlpha(0.2f);
    private static readonly Color?[] Dashes =
    [
        null,
        null,
        LathePalette.ScreenDim,
        LathePalette.ScreenLow,
        LathePalette.ScreenMid,
        LathePalette.Screen,
    ];
    private static readonly (int X, int Y, Color Color)[] Stuck =
    [
        (0, 0, LathePalette.ScreenHot),
        (2, 1, LathePalette.Screen),
        (-1, 3, LathePalette.Screen),
        (3, 4, LathePalette.ScreenLow),
        (1, -2, LathePalette.ScreenLow),
    ];

    private readonly LatheConsoleModel _model;

    public LatheScreenOverlay(LatheConsoleModel model)
    {
        _model = model;
        MouseFilter = MouseFilterMode.Ignore;
    }

    /// <summary>
    /// The screen row where the shifted slice of an emagged lathe begins. The torn strip lies right under the slice.
    /// </summary>
    public static int SliceTop(LatheConsoleModel model)
    {
        return TearFirst + model.Glitch(TearSalt, TearRows);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var scale = PixelCanvas.GetScale(UIScale);
        var dot = LatheScreenCanvas.GetDot(scale);
        if (_model.Emagged)
        {
            DrawTear(handle, scale);
            for (var i = 0; i < Cells; i++)
            {
                DrawCell(handle, scale, i);
            }

            foreach (var (x, y, color) in Stuck)
            {
                handle.DrawRect(UIBox2.FromDimensions((StuckX + x) * scale, (StuckY + y) * scale, scale, scale), color);
            }
        }

        for (var y = dot * (LinePeriod - 1); y < PixelSize.Y; y += dot * LinePeriod)
        {
            handle.DrawRect(UIBox2.FromDimensions(0f, y, PixelSize.X, dot), Raster);
        }
    }

    /// <summary>
    /// A strip where the picture is lost: dark, with scraps of signal of uneven length.
    /// </summary>
    private void DrawTear(DrawingHandleScreen handle, int scale)
    {
        var top = SliceTop(_model) + SliceHeight;
        handle.DrawRect(UIBox2.FromDimensions(0f, top * scale, PixelSize.X, TearHeight * scale), LathePalette.ScreenBack);

        var x = 0;
        for (var i = 0; x < LatheScreen.ArtWidth; i++)
        {
            var length = DashMin + _model.Glitch(DashSalt + i * 3, DashSpread);
            var row = _model.Glitch(DashSalt + i * 3 + 1, TearHeight);
            if (Dashes[_model.Glitch(DashSalt + i * 3 + 2, Dashes.Length)] is { } color)
                handle.DrawRect(UIBox2.FromDimensions(x * scale, (top + row) * scale, Math.Min(length, LatheScreen.ArtWidth - x) * scale, scale), color);

            x += length;
        }
    }

    /// <summary>
    /// A character cell filled with garbage.
    /// </summary>
    private void DrawCell(DrawingHandleScreen handle, int scale, int index)
    {
        var salt = CellSalt + index * 3;
        var left = _model.Glitch(salt, CellColumns) * CellGrid + 2;
        var top = CellTop + _model.Glitch(salt + 1, CellRows) * CellGrid;
        var kind = _model.Glitch(salt + 2, 3);
        handle.DrawRect(UIBox2.FromDimensions(left * scale, top * scale, CellWidth * scale, CellHeight * scale), LathePalette.ScreenBack);
        for (var y = 0; y < CellHeight; y++)
        {
            for (var x = 0; x < CellWidth; x++)
            {
                var lit = kind switch
                {
                    0 => (x + y) % 2 == 0,
                    1 => y % 2 == 0,
                    _ => x != 2 || y is 0 or CellHeight - 1,
                };

                if (lit)
                    handle.DrawRect(UIBox2.FromDimensions((left + x) * scale, (top + y) * scale, scale, scale), LathePalette.ScreenMid);
            }
        }
    }
}
