using System.Numerics;
using Content.Client._Polonium.UserInterface.PixelUi;
using Content.Client.Materials.UI;
using Content.Shared.Materials;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Lathe.UI;

/// <summary>
/// The stock of materials. Each one has a gauge: solid is what lies in store, the hatched top is what the pointed-at
/// recipe would take, the checker above is what the queue has already taken.
/// </summary>
public sealed partial class LatheMaterials : Control
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public const int ArtWidth = 234;
    public const int ArtHeight = 56;

    private const int CellWidth = 50;
    private const int CellPitch = 53;
    private const int GaugeWidth = 9;
    private const int GaugeHeight = 31;
    private const int GaugeInner = GaugeHeight - 2;
    private const int IconLeft = 13;
    private const int IconBox = 16;
    private const int NameTop = 17;
    private const int ReadoutTop = 32;
    private const int ReadoutHeight = 10;
    private const int KeyTop = 43;
    private const int KeyWidth = 16;
    private const int KeyHeight = 11;
    private const int KeyPitch = 17;
    private const int BarTop = 55;
    private const int MinThumb = 12;

    private static readonly int[] EjectSheets = [1, 5, 10];
    private static readonly UIBox2 Hatch = UIBox2.FromDimensions(0f, 0f, GaugeWidth - 2, GaugeInner);
    private static readonly UIBox2 Checker = UIBox2.FromDimensions(GaugeWidth - 2, 0f, GaugeWidth - 2, GaugeInner);

    private sealed class Cell
    {
        public MaterialPrototype Material = default!;
        public Texture Icon = default!;
        public string Name = string.Empty;
        public string Unit = string.Empty;
        public string Readout = string.Empty;
        public PixelKey[] Keys = default!;
        public int Volume = -1;
        public int Cost = -1;
        public int SheetVolume;
    }

    private readonly LatheConsoleModel _model;
    private readonly PixelFont _font;
    private readonly LatheTextures _textures;
    private readonly SpriteSystem _sprite;
    private readonly MaterialStorageUIController _controller;
    private readonly PixelKeySoundUIController _keySounds;
    private readonly List<Cell> _cells = new();
    private readonly string _empty;
    private int _scroll;

    public LatheMaterials(LatheConsoleModel model, PixelFont font, LatheTextures textures)
    {
        IoCManager.InjectDependencies(this);

        _model = model;
        _font = font;
        _textures = textures;
        _sprite = _entMan.System<SpriteSystem>();
        _controller = UserInterfaceManager.GetUIController<MaterialStorageUIController>();
        _keySounds = UserInterfaceManager.GetUIController<PixelKeySoundUIController>();
        _empty = Loc.GetString("lathe-menu-no-materials-message").ToUpperInvariant();

        MouseFilter = MouseFilterMode.Stop;
        RectClipContent = true;
        ToolTip = Loc.GetString("polonium-lathe-gauge-tooltip");
    }

    private int MaxScroll => Math.Max(0, _cells.Count * CellPitch - (CellPitch - CellWidth) - ArtWidth);

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var changed = false;
        for (var i = _cells.Count - 1; i >= 0; i--)
        {
            if (Shown(_cells[i].Material.ID))
                continue;

            foreach (var key in _cells[i].Keys)
            {
                key.Orphan();
            }

            _cells.RemoveAt(i);
            changed = true;
        }

        foreach (var id in _model.Materials.Keys)
        {
            changed |= TryAddCell(id);
        }

        foreach (var id in _model.Reserved.Keys)
        {
            changed |= TryAddCell(id);
        }

        if (changed)
        {
            _cells.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
            InvalidateArrange();
        }

        foreach (var cell in _cells)
        {
            Refresh(cell);
        }

        var scroll = Math.Clamp(_scroll, 0, MaxScroll);
        if (scroll == _scroll)
            return;

        _scroll = scroll;
        InvalidateArrange();
    }

    /// <summary>
    /// A material keeps its gauge while some of it lies in store or the queue has taken it, even all of it.
    /// </summary>
    private bool Shown(ProtoId<MaterialPrototype> material)
    {
        return _model.Materials.GetValueOrDefault(material) > 0 || _model.Reserved.GetValueOrDefault(material) > 0;
    }

    private bool TryAddCell(ProtoId<MaterialPrototype> id)
    {
        if (!Shown(id) || HasCell(id) || !_proto.Resolve(id, out var material))
            return false;

        _cells.Add(Create(material));
        return true;
    }

    private Cell Create(MaterialPrototype material)
    {
        var name = Loc.GetString(material.Name);
        var cell = new Cell
        {
            Material = material,
            Icon = _sprite.Frame0(material.Icon),
            Name = name.ToUpperInvariant(),
            Unit = Loc.GetString(material.Unit),
            SheetVolume = _model.SheetVolume(material),
            Keys = new PixelKey[EjectSheets.Length],
        };

        for (var i = 0; i < EjectSheets.Length; i++)
        {
            var sheets = EjectSheets[i];
            var key = new PixelKey
            {
                Skin = _textures.KeyLight,
                Font = _font,
                Text = sheets.ToString(),
                TextColor = LathePalette.KeyText,
                ToolTip = Loc.GetString("lathe-menu-tooltip-display", ("amount", sheets), ("material", name)),
            };
            key.OnPressed += _ => _controller.SendLatheEjectMessage(_model.Entity, material.ID, sheets);
            _keySounds.Attach(key);
            cell.Keys[i] = key;
            AddChild(key);
        }

        return cell;
    }

    private void Refresh(Cell cell)
    {
        var volume = _model.Materials.GetValueOrDefault(cell.Material.ID);
        var cost = 0;
        if (_model.Hover is { } hover && hover.Materials.TryGetValue(cell.Material.ID, out var amount))
            cost = _model.Cost(hover, amount) * _model.Amount;

        for (var i = 0; i < cell.Keys.Length; i++)
        {
            cell.Keys[i].Visible = _model.CanEject;
            cell.Keys[i].Disabled = volume < EjectSheets[i] * cell.SheetVolume;
        }

        if (volume == cell.Volume && cost == cell.Cost)
            return;

        cell.Volume = volume;
        cell.Cost = cost;

        float sheet = cell.SheetVolume;
        if (cost > 0)
            cell.Readout = Loc.GetString("polonium-lathe-material-use", ("amount", volume / sheet), ("cost", cost / sheet));
        else
            cell.Readout = Loc.GetString("lathe-menu-material-amount", ("amount", volume / sheet), ("unit", cell.Unit));
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);

        if (args.Delta.Y == 0f || MaxScroll == 0)
            return;

        _scroll = Math.Clamp(_scroll - MathF.Sign(args.Delta.Y) * CellPitch, 0, MaxScroll);
        InvalidateArrange();
        args.Handle();
    }

    private bool HasCell(ProtoId<MaterialPrototype> material)
    {
        foreach (var cell in _cells)
        {
            if (cell.Material.ID == material.Id)
                return true;
        }

        return false;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        for (var i = 0; i < _cells.Count; i++)
        {
            var left = i * CellPitch - _scroll;
            for (var k = 0; k < _cells[i].Keys.Length; k++)
            {
                _cells[i].Keys[k].Arrange(LatheLayout.Box(UIScale, left + k * KeyPitch, KeyTop, KeyWidth, KeyHeight));
            }
        }

        return finalSize;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var canvas = new PixelCanvas(handle, UIScale);
        if (_cells.Count == 0)
        {
            canvas.Text(_font, 1, 3, _empty, LathePalette.Faint);
            return;
        }

        for (var i = 0; i < _cells.Count; i++)
        {
            var left = i * CellPitch - _scroll;
            if (left < ArtWidth && left + CellWidth > 0)
                DrawCell(handle, canvas, _cells[i], left);
        }

        if (MaxScroll == 0)
            return;

        var thumb = Math.Max(MinThumb, ArtWidth * ArtWidth / (ArtWidth + MaxScroll));
        canvas.Rect((ArtWidth - thumb) * _scroll / MaxScroll, BarTop, thumb, 1, LathePalette.Thumb);
    }

    private void DrawCell(DrawingHandleScreen handle, PixelCanvas canvas, Cell cell, int left)
    {
        float sheet = cell.SheetVolume;
        var stock = Rows(cell.Volume / sheet);
        var use = cell.Cost > 0 ? Math.Clamp(Math.Max(1, Rows(cell.Cost / sheet)), 0, stock) : 0;
        var taken = Math.Min(GaugeInner - stock, Rows(_model.Reserved.GetValueOrDefault(cell.Material.ID) / sheet));

        canvas.Rect(left, 0, GaugeWidth, GaugeHeight, LathePalette.Outline);
        canvas.Rect(left + 1, 1, GaugeWidth - 2, GaugeInner, LathePalette.ScreenBack);
        var bottom = 1 + GaugeInner;
        canvas.Rect(left + 1, bottom - (stock - use), GaugeWidth - 2, stock - use, LathePalette.Screen);
        Pattern(canvas, Hatch, left + 1, bottom - stock, use);
        Pattern(canvas, Checker, left + 1, bottom - stock - taken, taken);

        var size = (Vector2) cell.Icon.Size * canvas.Scale / 2f;
        var center = new Vector2((left + IconLeft + IconBox / 2f) * canvas.Scale, IconBox * canvas.Scale / 2f);
        handle.DrawTextureRect(cell.Icon, UIBox2.FromDimensions((center - size / 2f).Floored(), size));

        var nameWidth = CellWidth - IconLeft;
        canvas.Text(_font, left + IconLeft, NameTop, cell.Name.AsSpan(0, _font.Fit(cell.Name, nameWidth)), LathePalette.Label);

        var (back, ink) = cell.Cost > 0
            ? (LathePalette.Screen, LathePalette.ScreenBack)
            : (LathePalette.ScreenBack, LathePalette.Screen);
        canvas.Rect(left, ReadoutTop, CellWidth, ReadoutHeight, back);
        canvas.Text(_font, left + 2, ReadoutTop + 1, cell.Readout.AsSpan(0, _font.Fit(cell.Readout, CellWidth - 4)), ink);
    }

    private static int Rows(float sheets)
    {
        return (int) MathF.Round(Math.Clamp(sheets / LatheConsoleModel.GaugeSheets, 0f, 1f) * GaugeInner);
    }

    /// <summary>
    /// The pattern is cut out of a strip as tall as the gauge, so it stays in place while the level moves.
    /// </summary>
    private void Pattern(PixelCanvas canvas, UIBox2 strip, int x, int y, int height)
    {
        if (height <= 0)
            return;

        var source = UIBox2.FromDimensions(strip.Left, y - 1, strip.Width, height);
        canvas.Region(_textures.Gauge, source, x, y);
    }
}
