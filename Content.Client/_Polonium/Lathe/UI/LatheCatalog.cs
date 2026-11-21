using System.Numerics;
using System.Text;
using Content.Client._Polonium.UserInterface.PixelUi;
using Content.Client.Lathe.UI;
using Content.Shared.Materials;
using Content.Shared.Research.Prototypes;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Polonium.Lathe.UI;

/// <summary>
/// One visible line of the catalog. The lines are a fixed set that is bound to recipes anew as the list scrolls.
/// </summary>
public sealed class LatheCatalogRow : Control
{
    private readonly LatheCatalog _catalog;

    public LatheRecipePrototype? Recipe;
    public string Title = string.Empty;

    public ProtoId<LatheRecipePrototype>? RecipeId => Recipe?.ID;

    public LatheCatalogRow(LatheCatalog catalog)
    {
        _catalog = catalog;
        MouseFilter = MouseFilterMode.Stop;
        DefaultCursorShape = CursorShape.Hand;
        TooltipSupplier = _ => Recipe == null ? null : new RecipeTooltip(_catalog.TooltipText(Recipe));
    }

    protected override void MouseEntered()
    {
        base.MouseEntered();

        _catalog.SetHovered(this);
    }

    protected override void MouseExited()
    {
        base.MouseExited();

        _catalog.ClearHovered(this);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick || Recipe == null)
            return;

        _catalog.Press(Recipe);
        args.Handle();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (Recipe != null && PixelSize.X > 0)
            _catalog.DrawRow(handle, this);
    }
}

public sealed partial class LatheCatalog : Control
{
    [Dependency] private IPrototypeManager _proto = default!;

    public const int RowHeight = 14;
    public const int VisibleRows = 7;

    private const int BarWidth = 6;
    private const int MinThumb = 6;
    private const int IconBox = 16;
    private const int Padding = 3;
    private const int NameLeft = 23;
    private const float UnavailableAlpha = 0.35f;

    private readonly LatheConsoleModel _model;
    private readonly LatheIconCache _icons;
    private readonly LatheScreenFont _font;
    private readonly LatheCatalogRow[] _rows = new LatheCatalogRow[VisibleRows];
    private readonly string _empty;

    private LatheCatalogRow? _hovered;
    private int _first;
    private bool _dragging;

    public event Action<LatheRecipePrototype>? OnPressed;

    public IReadOnlyList<LatheCatalogRow> Rows => _rows;

    public LatheCatalog(LatheConsoleModel model, LatheIconCache icons, LatheScreenFont font)
    {
        IoCManager.InjectDependencies(this);

        _model = model;
        _icons = icons;
        _font = font;
        _empty = Loc.GetString("polonium-lathe-catalog-empty");
        MouseFilter = MouseFilterMode.Stop;

        for (var i = 0; i < VisibleRows; i++)
        {
            _rows[i] = new LatheCatalogRow(this);
            AddChild(_rows[i]);
        }

        _model.CatalogChanged += OnCatalogChanged;
        Bind();
    }

    private int MaxFirst => Math.Max(0, _model.Catalog.Count - VisibleRows);

    private bool Scrollable => _model.Catalog.Count > VisibleRows;

    private float Unit => PixelCanvas.GetScale(UIScale) / UIScale;

    private void OnCatalogChanged()
    {
        _first = Math.Min(_first, MaxFirst);
        Bind();
        InvalidateArrange();
    }

    private void Bind()
    {
        for (var i = 0; i < VisibleRows; i++)
        {
            var row = _rows[i];
            var index = _first + i;
            if (index < _model.Catalog.Count)
            {
                row.Recipe = _model.Catalog[index].Recipe;
                row.Title = _model.Catalog[index].Name;
                row.Visible = true;
            }
            else
            {
                row.Recipe = null;
                row.Visible = false;
            }
        }

        _model.Hover = _hovered?.Recipe;
    }

    /// <summary>
    /// Whether the recipe is in the list but scrolled out of sight.
    /// </summary>
    public bool Hides(ProtoId<LatheRecipePrototype> recipe)
    {
        for (var i = 0; i < _model.Catalog.Count; i++)
        {
            if (_model.Catalog[i].Recipe.ID == recipe.Id)
                return i < _first || i >= _first + VisibleRows;
        }

        return false;
    }

    public void SetHovered(LatheCatalogRow row)
    {
        _hovered = row;
        _model.Hover = row.Recipe;
    }

    public void ClearHovered(LatheCatalogRow row)
    {
        if (_hovered != row)
            return;

        _hovered = null;
        _model.Hover = null;
    }

    public void Press(LatheRecipePrototype recipe)
    {
        OnPressed?.Invoke(recipe);
    }

    private void ScrollTo(int first)
    {
        first = Math.Clamp(first, 0, MaxFirst);
        if (first == _first)
            return;

        _first = first;
        Bind();
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);

        if (args.Delta.Y == 0f)
            return;

        ScrollTo(_first - MathF.Sign(args.Delta.Y));
        args.Handle();
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick || !Scrollable)
            return;

        _dragging = true;
        Drag(args.RelativePosition.Y);
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function == EngineKeyFunctions.UIClick)
            _dragging = false;
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        if (_dragging)
            Drag(args.RelativePosition.Y);
    }

    private void Drag(float y)
    {
        var height = VisibleRows * RowHeight;
        var thumb = ThumbHeight(height);
        var travel = (height - thumb) * Unit;
        if (travel <= 0f)
            return;

        var part = Math.Clamp((y - thumb * Unit / 2f) / travel, 0f, 1f);
        ScrollTo((int) MathF.Round(part * MaxFirst));
    }

    private int ThumbHeight(int height)
    {
        return Math.Max(MinThumb, height * VisibleRows / Math.Max(VisibleRows, _model.Catalog.Count));
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var width = (int) MathF.Round(finalSize.X / Unit) - (Scrollable ? BarWidth : 0);
        for (var i = 0; i < VisibleRows; i++)
        {
            _rows[i].Arrange(LatheLayout.Box(UIScale, 0, i * RowHeight, width, RowHeight));
        }

        return finalSize;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var canvas = new LatheScreenCanvas(handle, _font, UIScale);
        var width = PixelSize.X / canvas.Scale;
        if (_model.Catalog.Count == 0)
        {
            canvas.Text(5, 4, LathePalette.ScreenRow, width - 10, _empty, LathePalette.ScreenMid);
            return;
        }

        if (!Scrollable)
            return;

        var height = VisibleRows * RowHeight;
        var thumb = ThumbHeight(height);
        var top = MaxFirst == 0 ? 0 : (height - thumb) * _first / MaxFirst;
        canvas.Rect(width - BarWidth + 1, top + 1, BarWidth - 2, thumb - 2, LathePalette.ScreenLow);
    }

    public void DrawRow(DrawingHandleScreen handle, LatheCatalogRow row)
    {
        var recipe = row.Recipe!;
        var canvas = new LatheScreenCanvas(handle, _font, UIScale);
        var width = row.PixelSize.X / canvas.Scale;
        var available = _model.CanProduce(recipe);
        var hovered = _hovered == row;
        if (hovered)
            canvas.Rect(0, 0, width, RowHeight, available ? LathePalette.RowHover : LathePalette.RowHoverOff);

        var icon = _icons.Get(recipe);
        if (icon.Texture is { } texture)
        {
            var size = (Vector2) icon.Size * canvas.Scale / 2f;
            var center = new Vector2((Padding + IconBox / 2f) * canvas.Scale, RowHeight * canvas.Scale / 2f);
            var modulate = available ? Color.White : Color.White.WithAlpha(UnavailableAlpha);
            handle.DrawTextureRect(texture, UIBox2.FromDimensions((center - size / 2f).Floored(), size), modulate);
        }

        var color = !available ? LathePalette.ScreenLow : hovered ? LathePalette.ScreenHot : LathePalette.Screen;
        canvas.Text(NameLeft, 0, RowHeight, width - NameLeft - Padding, row.Title, color);
    }

    public string TooltipText(LatheRecipePrototype recipe)
    {
        var text = new StringBuilder();
        text.Append("[bold]").Append(FormattedMessage.EscapeText(_model.RecipeName(recipe))).Append("[/bold]");
        foreach (var (id, amount) in recipe.Materials)
        {
            if (!_proto.Resolve(id, out MaterialPrototype? material))
                continue;

            var cost = _model.Cost(recipe, amount) * _model.Amount;
            float volume = _model.SheetVolume(material);
            var unit = Loc.GetString(material.Unit);
            var name = Loc.GetString(material.Name);
            var missing = Math.Max(0, cost - _model.Materials.GetValueOrDefault(id));
            text.Append('\n');
            if (missing > 0)
            {
                text.Append(Loc.GetString("lathe-menu-material-amount-missing",
                    ("amount", cost / volume),
                    ("missingAmount", missing / volume),
                    ("unit", unit),
                    ("material", name)));
            }
            else
            {
                var amountText = Loc.GetString("lathe-menu-material-amount", ("amount", cost / volume), ("unit", unit));
                text.Append(Loc.GetString("lathe-menu-tooltip-display", ("material", name), ("amount", amountText)));
            }
        }

        var description = _model.RecipeDescription(recipe);
        if (!string.IsNullOrWhiteSpace(description))
            text.Append('\n').Append(Loc.GetString("lathe-menu-description-display", ("description", description)));

        return text.ToString();
    }
}
