using System.Numerics;
using Content.Client._Polonium.Lathe.UI;
using Content.Client._Polonium.UserInterface.PixelUi;
using Content.Client.Resources;
using Content.Shared._Polonium.Chemistry;
using Content.Shared.CCVar;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Chemistry.UI;

/// <summary>
/// A bar dispenser drawn as the device it is on its sprite, with a note of what is in the vessel pinned next to it.
/// </summary>
public sealed partial class BarDispenserWindow : BaseWindow
{
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private ILogManager _log = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IResourceCache _cache = default!;

    private const string KeySkinPath = "/Textures/_Polonium/Interface/PixelUi/key_dark.png";

    private const int Left = BarDispenserArt.BodyLeft;
    private const int ArtWidth = BarDispenserArt.BodyWidth;
    private const int Top = BarDispenserArt.Top;
    private const int Columns = BarDispenserArt.KeyColumns;
    private const int KeysLeft = Left + 8;
    private const int SideAxis = (ArtWidth - BarDispenserArt.BayWidth) / 4;
    private const int DoseAxis = Left + SideAxis;
    private const int RoundAxis = Left + ArtWidth - SideAxis;
    private const int RoundSpread = 18;
    private const int RoundRadius = 9;
    private const int RoundTop = 20;
    private const int RoundPitch = 30;
    private const int DoseTop = 10;
    private const int DoseKeysTop = 21;
    private const int NoteLeft = Left + ArtWidth + 6;
    private const int NoteTop = 4;
    private const int NoteWidth = 110;
    private const int NoteHeight = 120;
    private const int NotePin = 4;
    private const int NoteHeading = 11;
    private const int NoteRows = 10;
    private const int CanvasWidth = NoteLeft + NoteWidth + 4;
    private const int LeastHeight = 132;
    private const int LeavingAxis = 20;
    private const int LeavingFoot = 10;

    private static readonly ReagentDispenserDispenseAmount[] Amounts =
    [
        ReagentDispenserDispenseAmount.U1,
        ReagentDispenserDispenseAmount.U5,
        ReagentDispenserDispenseAmount.U10,
        ReagentDispenserDispenseAmount.U15,
        ReagentDispenserDispenseAmount.U20,
        ReagentDispenserDispenseAmount.U30,
        ReagentDispenserDispenseAmount.U40,
        ReagentDispenserDispenseAmount.U60,
        ReagentDispenserDispenseAmount.U120,
    ];

    private static readonly Color DosePrint = Color.FromHex("#f2f2f2");

    private static readonly LocId DoseTip = "polonium-bar-dispenser-dose";
    private static readonly LocId DrinkTip = "polonium-bar-dispenser-drink";
    private static readonly LocId EmptyDrinkTip = "polonium-bar-dispenser-drink-empty";
    private static readonly LocId TakeDrinkTip = "polonium-bar-dispenser-drink-take";
    private static readonly LocId ReagentRow = "polonium-bar-dispenser-note-reagent";
    private static readonly LocId VolumeTally = "polonium-bar-dispenser-note-volume";

    private readonly PixelFont _font;
    private readonly LatheScreenFont _nameFont;
    private readonly Texture _controls;
    private readonly PixelKeySoundUIController _keySounds;
    private readonly BarSpriteProbe _probe;
    private readonly BarEntityPainter _painter;
    private readonly BarBottleShelf _shelf;
    private readonly BarDispenserBay _bay;
    private readonly BarLeavingLayer _leaving;
    private readonly PixelKey[] _doseKeys = new PixelKey[Amounts.Length];
    private readonly BarRoundKey _drain;
    private readonly BarRoundKey _eject;
    private readonly BarRoundKey _service;
    private readonly BarRoundKey _close;
    private readonly PixelNote _note;
    private readonly PixelNoteSection _contents;
    private readonly List<PixelNoteRow> _rows = new();
    private readonly List<BarDrinkKey> _keys = new();
    private readonly Caption _doseCaption;
    private readonly Caption _drainCaption;
    private readonly Caption _ejectCaption;
    private readonly Caption _serviceCaption;
    private readonly Caption _closeCaption;
    private readonly string _drainTip;
    private readonly string _ejectTip;
    private readonly string _noVessel;
    private readonly string _vesselEmpty;
    private readonly string _vesselFull;
    private readonly string _containerEmpty;
    private readonly string _handsFull;
    private readonly string _emptyText;

    private BarDispenserModel? _model;
    private BarDispenserSkinPrototype? _skin;
    private BarDrink? _armed;
    private EntityUid _owner;
    private ReagentDispenserDispenseAmount _amount = ReagentDispenserDispenseAmount.U10;
    private int _rowsTotal = 1;
    private int _rowsShown = 1;
    private int _scroll;
    private int _noteStamp;
    private bool _noteWritten;
    private bool _serviceMode;
    private bool _reduced;

    public event Action<ItemStorageLocation>? OnDispense;
    public event Action<ItemStorageLocation>? OnEject;
    public event Action<ReagentDispenserDispenseAmount>? OnAmountSelected;
    public event Action? OnClear;

    /// <summary>
    /// Raised by a click on the vessel, on its empty place or on the key that takes the vessel out.
    /// It carries true to put in the vessel from the hand and false to take the vessel out.
    /// </summary>
    public event Action<bool>? OnVesselPressed;

    public BarDispenserWindow()
    {
        IoCManager.InjectDependencies(this);

        _font = PixelFont.Tiny5(_cache);
        _nameFont = new LatheScreenFont(_cache);
        _controls = _cache.GetTexture(BarDispenserArt.TexturePath + "controls.png");
        _keySounds = UserInterfaceManager.GetUIController<PixelKeySoundUIController>();
        _probe = new BarSpriteProbe(_entMan, _clyde);
        _painter = new BarEntityPainter(_entMan, _clyde);

        _doseCaption = new Caption(_font, Loc.GetString("polonium-bar-dispenser-caption-dose"));
        _drainCaption = new Caption(_font, Loc.GetString("polonium-bar-dispenser-caption-drain"));
        _ejectCaption = new Caption(_font, Loc.GetString("polonium-bar-dispenser-caption-eject"));
        _serviceCaption = new Caption(_font, Loc.GetString("polonium-bar-dispenser-caption-service"));
        _closeCaption = new Caption(_font, Loc.GetString("polonium-bar-dispenser-caption-close"));
        _drainTip = Loc.GetString("polonium-bar-dispenser-drain");
        _ejectTip = Loc.GetString("polonium-bar-dispenser-eject");
        _noVessel = Loc.GetString("polonium-bar-dispenser-no-vessel");
        _vesselEmpty = Loc.GetString("polonium-bar-dispenser-vessel-empty");
        _vesselFull = Loc.GetString("polonium-bar-dispenser-vessel-full");
        _containerEmpty = Loc.GetString("polonium-bar-dispenser-container-empty");
        _handsFull = Loc.GetString("polonium-bar-dispenser-hands-full");
        _emptyText = Loc.GetString("polonium-bar-dispenser-note-empty");

        MouseFilter = MouseFilterMode.Stop;
        Resizable = false;

        _shelf = new BarBottleShelf(_probe, _painter) { Visible = false };
        _shelf.OnPressed += PressDrink;
        AddChild(_shelf);

        _bay = new BarDispenserBay(_probe, _painter);
        _bay.OnPressed += insert => OnVesselPressed?.Invoke(insert);
        _bay.OnCue += PlayCue;
        AddChild(_bay);

        var keySkin = _cache.GetTexture(KeySkinPath);
        for (var i = 0; i < Amounts.Length; i++)
        {
            var amount = Amounts[i];
            var key = new PixelKey
            {
                Skin = keySkin,
                Font = _font,
                Text = ((int) amount).ToString(),
                TextColor = DosePrint,
                ToolTip = Loc.GetString(DoseTip, ("amount", (int) amount)),
            };
            key.OnPressed += _ =>
            {
                _amount = amount;
                OnAmountSelected?.Invoke(amount);
            };
            _keySounds.Attach(key);
            AddChild(key);
            _doseKeys[i] = key;
        }

        _drain = AddRoundKey(BarDispenserArt.DrainIcon);
        _drain.OnPressed += _ => OnClear?.Invoke();

        _eject = AddRoundKey(BarDispenserArt.EjectIcon);
        _eject.OnPressed += _ => OnVesselPressed?.Invoke(false);

        _service = AddRoundKey(BarDispenserArt.ServiceIcon);
        _service.ToolTip = Loc.GetString("polonium-bar-dispenser-service");
        _service.OnPressed += _ =>
        {
            _serviceMode = !_serviceMode;
            WriteTips();
        };

        _close = AddRoundKey(BarDispenserArt.CloseIcon);
        _close.ToolTip = Loc.GetString("polonium-bar-dispenser-close");
        _close.OnPressed += _ => Close();

        _note = new PixelNote(_cache.GetTexture(BarDispenserArt.TexturePath + "note.png"), PixelFont.Plain(_cache), NoteWidth)
        {
            MouseFilter = MouseFilterMode.Pass,
            Interactive = true,
        };
        _contents = _note.AddSection(NoteHeading, NoteRows);
        _contents.More = Loc.GetString("polonium-bar-dispenser-note-more");
        _contents.End = Loc.GetString("polonium-bar-dispenser-note-end");
        AddChild(_note);

        _leaving = new BarLeavingLayer(_probe, _painter);
        AddChild(_leaving);
    }

    private int BodyTop => Top + (_skin is { ShowContainers: true } ? BarBottleShelf.ArtHeight : BarDispenserArt.LidHeight) - 1;
    private int PanelTop => BodyTop + 3;
    private int PanelHeight => _rowsShown * BarDispenserArt.KeyPitchY + 7;
    private int BandTop => PanelTop + PanelHeight + 3;
    private int Bottom => BandTop + BarDispenserArt.BandHeight + 3;
    private int CanvasHeight => Math.Max(Bottom + 3, LeastHeight);

    /// <summary>
    /// Tells the window which dispenser it shows.
    /// </summary>
    public void SetEntity(EntityUid dispenser)
    {
        if (!_entMan.TryGetComponent(dispenser, out BarDispenserSkinComponent? look) || !_proto.TryIndex(look.Skin, out var skin))
        {
            _log.GetSawmill("ui.bar_dispenser").Error($"{_entMan.ToPrettyString(dispenser)} has no skin for its bar dispenser window.");
            return;
        }

        _owner = dispenser;
        _skin = skin;
        _shelf.Skin = skin;
        _shelf.Visible = skin.ShowContainers;
        _model = new BarDispenserModel(dispenser, skin, _entMan, _proto, _player);
        _model.OnPoured += drink => _bay.Pour(drink.Color, drink.Kind);
        _model.OnDrained += _bay.Drain;
        _model.OnRemoved += ShowLeaving;

        // The server lists the containers a moment later, the body takes its height from their number right away.
        CountRows(_model.StoredCount());
    }

    /// <summary>
    /// Takes in what the server says about the dispenser.
    /// </summary>
    public void UpdateState(ReagentDispenserBoundUserInterfaceState state)
    {
        if (_model is not { } model)
            return;

        model.SetState(state);
        _amount = model.Amount;

        var drinks = model.Drinks;
        while (_keys.Count < drinks.Count)
        {
            var key = new BarDrinkKey(_nameFont) { Skin = _skin };
            // The keys are handed out anew with every state, so a key may stand for another drink by the time it is let go.
            key.OnButtonDown += _ => _armed = key.Drink;
            key.OnPressed += _ =>
            {
                if (key.Drink == _armed)
                    PressDrink(key.Drink);
            };
            _keySounds.Attach(key);
            AddChild(key);
            _keys.Add(key);
        }

        while (_keys.Count > drinks.Count)
        {
            RemoveChild(_keys[^1]);
            _keys.RemoveAt(_keys.Count - 1);
        }

        for (var i = 0; i < drinks.Count; i++)
        {
            _keys[i].Drink = drinks[i];
        }

        _leaving.SetPositionLast();
        CountRows(drinks.Count);
        WriteTips();
    }

    private void CountRows(int drinks)
    {
        _rowsTotal = Math.Max(1, (drinks + Columns - 1) / Columns);
        _rowsShown = Math.Min(_rowsTotal, BarDispenserArt.MaxKeyRows);
        ScrollTo(_scroll);
        InvalidateMeasure();
    }

    private void ScrollTo(int row)
    {
        _scroll = Math.Clamp(row, 0, _rowsTotal - _rowsShown);
        for (var i = 0; i < _keys.Count; i++)
        {
            var shown = i / Columns - _scroll;
            _keys[i].Visible = shown >= 0 && shown < _rowsShown;
        }

        InvalidateArrange();
    }

    private void WriteTips()
    {
        if (_model is not { } model)
            return;

        foreach (var drink in model.Drinks)
        {
            drink.Tip = _serviceMode ? Loc.GetString(TakeDrinkTip, ("name", drink.Name))
                : drink.Quantity <= 0f ? Loc.GetString(EmptyDrinkTip, ("name", drink.Name))
                : Loc.GetString(DrinkTip, ("name", drink.Name), ("amount", Math.Round((double) drink.Quantity, 2)));
        }

        foreach (var key in _keys)
        {
            key.Service = _serviceMode;
            key.SetLiveToolTip(key.Drink?.Tip);
        }
    }

    private BarRoundKey AddRoundKey(UIBox2 icon)
    {
        var key = new BarRoundKey { Atlas = _controls, Icon = icon };
        _keySounds.Attach(key);
        AddChild(key);
        return key;
    }

    private void PressDrink(BarDrink? drink)
    {
        if (drink == null || _model is not { } model)
            return;

        if (_serviceMode)
        {
            if (model.HasFreeHand())
                OnEject?.Invoke(model.Locate(drink));
            else
                Explain(_handsFull);

            return;
        }

        if (model.Vessel == null)
            Explain(_noVessel);
        else if (drink.Quantity <= 0f)
            Explain(_containerEmpty);
        else if (model.VesselSolution is { } solution && solution.AvailableVolume <= 0)
            Explain(_vesselFull);
        else
            OnDispense?.Invoke(model.Locate(drink));
    }

    /// <summary>
    /// Tells the player why a press did nothing, the way the game does: with a popup over the dispenser.
    /// </summary>
    private void Explain(string reason)
    {
        _entMan.System<SharedPopupSystem>().PopupEntity(reason, _owner);
    }

    private void PlayCue(BarBayCue cue)
    {
        if (_skin is not { } skin)
            return;

        Play(cue switch
        {
            BarBayCue.Pour => skin.PourSound,
            BarBayCue.Fizz => skin.FizzSound,
            BarBayCue.Cubes => skin.CubesSound,
            BarBayCue.Grains => skin.GrainsSound,
            BarBayCue.Drain => skin.DrainSound,
            BarBayCue.VesselIn => skin.VesselInSound,
            _ => skin.VesselOutSound,
        });
    }

    /// <summary>
    /// Plays a sound of the dispenser at the dispenser, so it follows the volume of the game and not of the interface.
    /// </summary>
    private void Play(SoundSpecifier? sound)
    {
        if (sound != null)
            _entMan.System<SharedAudioSystem>().PlayEntity(sound, Filter.Local(), _owner, false);
    }

    private void ShowLeaving(BarDrink drink)
    {
        Play(_skin?.ContainerOutSound);
        if (_reduced || _model is not { } model || _skin is not { } skin || drink.Container is not { } container)
            return;

        var drinks = model.Drinks;
        for (var i = 0; i < drinks.Count; i++)
        {
            if (drinks[i] != drink)
                continue;

            var row = i / Columns - _scroll;
            if (skin.ShowContainers)
                _leaving.Add(container, Left + BarBottleShelf.AxisOf(i, drinks.Count), Top + BarBottleShelf.Tip, true);
            else if (row >= 0 && row < _rowsShown)
                _leaving.Add(container, KeysLeft + i % Columns * BarDispenserArt.KeyPitchX + LeavingAxis, KeyTop(row) + LeavingFoot, false);

            return;
        }
    }

    private int KeyTop(int row)
    {
        return PanelTop + 4 + row * BarDispenserArt.KeyPitchY;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_model is not { } model)
            return;

        _reduced = _cfg.GetCVar(CCVars.ReducedMotion);
        model.Refresh(args.DeltaSeconds);
        _bay.Advance(model, args.DeltaSeconds, _reduced);
        _shelf.Advance(model.Drinks, _reduced);
        _leaving.Advance(args.DeltaSeconds, _reduced);
        _note.ReducedMotion = _reduced;
        UpdateControls(model);
        UpdateNote(model);
    }

    private void UpdateControls(BarDispenserModel model)
    {
        for (var i = 0; i < Amounts.Length; i++)
        {
            var chosen = Amounts[i] == _amount;
            _doseKeys[i].Latched = chosen;
            _doseKeys[i].TextColor = chosen ? BarDispenserArt.LampGreen : DosePrint;
        }

        var filled = model.VesselSolution is { Volume.Value: > 0 };
        _drain.Disabled = !filled || _bay.Draining;
        _drain.SetLiveToolTip(model.Vessel == null ? _noVessel : filled ? _drainTip : _vesselEmpty);
        _eject.Disabled = model.Vessel == null;
        _eject.SetLiveToolTip(model.Vessel == null ? _noVessel : _ejectTip);
        _service.Latched = _serviceMode;

        // A container and its key light up together, whichever of the two the pointer is on.
        var pointed = _shelf.Hovered;
        foreach (var key in _keys)
        {
            if (key.IsHovered && key.Visible)
                pointed = key.Drink;
        }

        _shelf.Highlighted = pointed;
        foreach (var key in _keys)
        {
            key.Highlighted = pointed != null && key.Drink == pointed;
        }
    }

    private void UpdateNote(BarDispenserModel model)
    {
        var stamp = new HashCode();
        stamp.Add(model.Vessel);
        var name = string.Empty;
        if (model.Vessel is { } vessel && _entMan.TryGetComponent(vessel, out MetaDataComponent? meta))
            name = meta.EntityName;

        stamp.Add(name);
        if (model.VesselSolution is { } mix)
        {
            stamp.Add(mix.MaxVolume);
            foreach (var (reagent, quantity) in mix.Contents)
            {
                stamp.Add(reagent.Prototype);
                stamp.Add(quantity);
            }
        }

        var hash = stamp.ToHashCode();
        if (_noteWritten && hash == _noteStamp)
            return;

        _noteWritten = true;
        _noteStamp = hash;

        _rows.Clear();
        _contents.Dimmed = model.Vessel == null;
        _contents.Heading = model.Vessel == null ? _noVessel : name;
        _contents.Empty = model.Vessel == null ? string.Empty : _emptyText;
        _contents.Tally = string.Empty;
        if (model.VesselSolution is { } solution)
        {
            _contents.Tally = Loc.GetString(VolumeTally, ("volume", solution.Volume.Double()), ("capacity", solution.MaxVolume.Double()));
            foreach (var (reagent, quantity) in solution.Contents)
            {
                if (quantity <= 0 || !_proto.TryIndex<ReagentPrototype>(reagent.Prototype, out var prototype))
                    continue;

                var text = Loc.GetString(ReagentRow, ("amount", quantity.Double()), ("name", prototype.LocalizedName));
                _rows.Add(new PixelNoteRow(EntityUid.Invalid, text, prototype.SubstanceColor));
            }
        }

        _note.SetRows(_contents, _rows);
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
    {
        return DragMode.Move;
    }

    protected override bool HasPoint(Vector2 point)
    {
        var art = ToArt(point);
        return UIBox2i.FromDimensions(Left, Top, ArtWidth, Bottom - Top).Contains(art)
               || UIBox2i.FromDimensions(NoteLeft, NoteTop + NotePin, NoteWidth, NoteHeight - NotePin).Contains(art);
    }

    private Vector2i ToArt(Vector2 point)
    {
        return (Vector2i) (point * UIScale / PixelCanvas.GetScale(UIScale));
    }

    protected override void MouseWheel(GUIMouseWheelEventArgs args)
    {
        base.MouseWheel(args);

        if (args.Handled || args.Delta.Y == 0f || _rowsTotal <= _rowsShown)
            return;

        if (!UIBox2i.FromDimensions(Left + 5, PanelTop, ArtWidth - 10, PanelHeight).Contains(ToArt(args.RelativePosition)))
            return;

        ScrollTo(_scroll + (args.Delta.Y < 0f ? 1 : -1));
        args.Handle();
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(availableSize);
        }

        return PixelLayout.Box(UIScale, 0, 0, CanvasWidth, CanvasHeight).Size;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        Place(_shelf, Left, Top, ArtWidth, BarBottleShelf.ArtHeight);
        for (var i = 0; i < _keys.Count; i++)
        {
            Place(_keys[i], KeysLeft + i % Columns * BarDispenserArt.KeyPitchX, KeyTop(i / Columns - _scroll), BarDispenserArt.KeyWidth, BarDispenserArt.KeyHeight);
        }

        var band = BandTop;
        Place(_bay, Left + (ArtWidth - BarDispenserArt.BayWidth) / 2, band + 4, BarDispenserArt.BayWidth, BarDispenserArt.BayHeight);
        for (var i = 0; i < _doseKeys.Length; i++)
        {
            Place(_doseKeys[i], DoseAxis - 26 + i % 3 * 18, band + DoseKeysTop + i / 3 * 13, 17, 12);
        }

        PlaceRound(_drain, RoundAxis - RoundSpread, band + RoundTop);
        PlaceRound(_eject, RoundAxis + RoundSpread, band + RoundTop);
        PlaceRound(_service, RoundAxis - RoundSpread, band + RoundTop + RoundPitch);
        PlaceRound(_close, RoundAxis + RoundSpread, band + RoundTop + RoundPitch);

        Place(_note, NoteLeft, NoteTop, NoteWidth, NoteHeight);
        Place(_leaving, 0, 0, CanvasWidth, CanvasHeight);
        return finalSize;
    }

    private void Place(Control control, int left, int top, int width, int height)
    {
        control.Arrange(PixelLayout.Box(UIScale, left, top, width, height));
    }

    private void PlaceRound(Control key, int centerX, int centerY)
    {
        Place(key, centerX - RoundRadius, centerY - RoundRadius, RoundRadius * 2 + 1, RoundRadius * 2 + 1);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        _probe.Render(handle);
        if (_skin is not { } skin)
            return;

        var canvas = new PixelCanvas(handle, UIScale);
        var ink = BarDispenserArt.Ink;
        if (!skin.ShowContainers)
        {
            canvas.Rect(Left, Top, ArtWidth, BarDispenserArt.LidHeight, ink);
            canvas.Rect(Left + 1, Top + 1, ArtWidth - 2, BarDispenserArt.LidHeight - 1, skin.BodyLight);
            canvas.Rect(Left + 1, Top + 1, ArtWidth - 2, 1, skin.BodyBright);
            canvas.Rect(Left + 1, Top + BarDispenserArt.LidHeight - 2, ArtWidth - 2, 1, skin.Body);
        }

        var top = BodyTop;
        var height = Bottom - top;
        canvas.Rect(Left, top, ArtWidth, height, ink);
        canvas.Rect(Left + 1, top + 1, ArtWidth - 2, height - 2, skin.Body);
        canvas.Rect(Left + 1, top + 1, ArtWidth - 2, 1, skin.BodyLight);
        canvas.Rect(Left + 1, top + height - 3, ArtWidth - 2, 2, skin.BodyShade);

        var band = BandTop;
        canvas.Rect(Left + 1, band - 1, ArtWidth - 2, 1, skin.BodyShade);
        canvas.Rect(Left + 1, band, ArtWidth - 2, 1, skin.BodyLight);

        canvas.Rect(Left + 5, PanelTop, ArtWidth - 10, PanelHeight, ink);
        canvas.Rect(Left + 6, PanelTop + 1, ArtWidth - 12, PanelHeight - 2, skin.Well);
        if (_rowsTotal > _rowsShown)
        {
            // More rows than the panel shows: a thin slider at its right edge tells where the shown ones are.
            var track = PanelHeight - 4;
            var slider = Math.Max(3, track * _rowsShown / _rowsTotal);
            var offset = (track - slider) * _scroll / (_rowsTotal - _rowsShown);
            canvas.Rect(Left + ArtWidth - 7, PanelTop + 2 + offset, 1, slider, skin.KeyShade);
        }

        var captions = band + RoundTop + RoundRadius + 2;
        _doseCaption.Draw(canvas, DoseAxis, band + DoseTop, skin.Print);
        _drainCaption.Draw(canvas, RoundAxis - RoundSpread, captions, skin.Print);
        _ejectCaption.Draw(canvas, RoundAxis + RoundSpread, captions, skin.Print);
        _serviceCaption.Draw(canvas, RoundAxis - RoundSpread, captions + RoundPitch, skin.Print);
        _closeCaption.Draw(canvas, RoundAxis + RoundSpread, captions + RoundPitch, skin.Print);
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();

        _probe.Release();
        _painter.Release();
    }

    /// <summary>
    /// A word printed on the body, centred on a column.
    /// </summary>
    private readonly struct Caption(PixelFont font, string text)
    {
        private readonly int _width = font.Measure(text) - 1;

        /// <param name="top">The row the tops of the capital letters stand on.</param>
        public void Draw(PixelCanvas canvas, int axis, int top, Color color)
        {
            canvas.Text(font, axis - _width / 2, top - font.CapTop, text, color);
        }
    }
}
