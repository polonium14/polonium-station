using System.Numerics;
using Content.Client._Polonium.UserInterface.PixelUi;
using Content.Client.Guidebook;
using Content.Client.Guidebook.Components;
using Content.Client.Resources;
using Content.Shared.Lathe;
using Content.Shared.Research.Prototypes;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Lathe.UI;

/// <summary>
/// The lathe as an operator console: a green screen with the catalog and a plotter, a rack of paper tickets
/// for the queue, material gauges, an emergency stop and, on lathes that take lube, a tank on the side.
/// </summary>
public sealed partial class LathePulpitWindow : BaseWindow
{
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IResourceCache _cache = default!;

    private const int TankGap = 37;
    private const int TankTop = 80;
    private const int BarTop = 8;
    private const int BarHeight = 18;
    private const int BarGap = 6;
    private const int PlateLeft = 8;
    private const int PlateMargin = 3;
    private const int PlatePadding = 8;
    private const int LampSize = 5;
    private const int LampGap = 3;
    private const int LabelLine = 9;
    private const int SoftTop = 181;
    private const int SoftHeight = 13;
    private const int SoftGap = 4;
    private const int SoftArrowWidth = 22;
    private const float RepeatDelay = 0.38f;
    private const float RepeatTime = 0.06f;
    private const int RepeatsBeforeTens = 24;
    private const float LatchTime = 0.11f;
    private const float StopHoldTime = 1f;

    private static readonly ProtoId<SoundCollectionPrototype> StopSounds = "LatheConsoleStop";
    private static readonly ProtoId<SoundCollectionPrototype> StopAllSounds = "LatheConsoleStopAll";
    private static readonly ProtoId<SoundCollectionPrototype> DenySounds = "LatheConsoleDeny";

    private static readonly Vector2i ConsoleSize = new(450, 290);
    private static readonly UIBox2 PlateFrame = UIBox2.FromDimensions(0f, 0f, 8f, 8f);
    private static readonly UIBox2i ScreenArea = UIBox2i.FromDimensions(19, 41, LatheScreen.ArtWidth, LatheScreen.ArtHeight);
    private static readonly int[] SoftLeft = [20, 46, 104, 161, 219, 276];
    private static readonly int[] SoftWidth = [SoftArrowWidth, 54, 53, 54, 53, SoftArrowWidth];
    private static readonly UIBox2i RackArea = UIBox2i.FromDimensions(319, 44, LatheTicketRack.ArtWidth, LatheTicketRack.ArtHeight);
    private static readonly UIBox2i TrayArea = UIBox2i.FromDimensions(321, 174, LatheTray.ArtWidth, LatheTray.ArtHeight);
    private static readonly UIBox2i MinusKey = UIBox2i.FromDimensions(14, 222, 17, 20);
    private static readonly UIBox2i AmountArea = UIBox2i.FromDimensions(36, 225, 44, 14);
    private static readonly UIBox2i PlusKey = UIBox2i.FromDimensions(85, 222, 17, 20);
    private static readonly UIBox2i ClearKey = UIBox2i.FromDimensions(14, 263, 17, 14);
    private static readonly UIBox2i ServersKey = UIBox2i.FromDimensions(33, 263, 69, 14);
    private static readonly UIBox2i MaterialsArea = UIBox2i.FromDimensions(118, 221, LatheMaterials.ArtWidth, LatheMaterials.ArtHeight);
    private static readonly UIBox2i StopArea = UIBox2i.FromDimensions(382, 224, LatheStopButton.ArtSize, LatheStopButton.ArtSize);
    private static readonly UIBox2i CloseKey = UIBox2i.FromDimensions(428, 10, 14, 14);
    private static readonly UIBox2i HelpKey = UIBox2i.FromDimensions(408, 10, 14, 14);
    private static readonly Vector2i QueueLabel = new(319, 33);
    private static readonly Vector2i AmountLabel = new(14, 211);
    private static readonly Vector2i MaterialsLabel = new(118, 211);

    private readonly PixelFont _font;
    private readonly LatheScreenFont _screenFont;
    private readonly PixelFont _plateFont;
    private readonly LatheTextures _textures;
    private readonly PixelKeySoundUIController _keySounds;
    private readonly SoundSpecifier _stopSound = new SoundCollectionSpecifier(StopSounds);
    private readonly SoundSpecifier _stopAllSound = new SoundCollectionSpecifier(StopAllSounds);
    private readonly SoundSpecifier _denySound = new SoundCollectionSpecifier(DenySounds);
    private readonly PixelKey[] _softKeys = new PixelKey[LatheScreen.SoftSlots + 2];
    private readonly string _queueLabel;
    private readonly string _amountLabel;
    private readonly string _materialsLabel;
    private readonly string[] _lampLabels;
    private readonly int[] _lampWidths;

    private LatheConsoleModel? _model;
    private LatheIconCache? _icons;
    private LatheScreen _screen = default!;
    private LatheTicketRack _rack = default!;
    private LatheTray _tray = default!;
    private LatheMaterials _materials = default!;
    private LatheTextField _amount = default!;
    private LatheTank? _tank;
    private LatheStopButton _stop = default!;
    private PixelKey _minus = default!;
    private PixelKey _plus = default!;
    private PixelKey _clear = default!;
    private PixelKey _servers = default!;
    private PixelKey _close = default!;
    private PixelKey? _help;

    private string _title = string.Empty;
    private (int Index, int Total)? _batch;
    private int _repeat;
    private int _repeats;
    private float _repeatClock;
    private PixelKey? _latched;
    private float _latchClock;
    private float _stopHeld;
    private bool _stoppedAll;

    public event Action<string, int>? OnQueue;
    public event Action<int>? OnDelete;
    public event Action<int, int>? OnMove;
    public event Action? OnAbort;
    public event Action? OnStopAll;
    public event Action? OnServerList;

    public LathePulpitWindow()
    {
        IoCManager.InjectDependencies(this);

        _font = PixelFont.Tiny5(_cache);
        _screenFont = new LatheScreenFont(_cache);
        _plateFont = _screenFont.Coarse;
        _textures = new LatheTextures(_cache);
        _keySounds = UserInterfaceManager.GetUIController<PixelKeySoundUIController>();

        _queueLabel = Loc.GetString("lathe-menu-queue-title").ToUpperInvariant();
        _amountLabel = Loc.GetString("polonium-lathe-amount").ToUpperInvariant();
        _materialsLabel = Loc.GetString("lathe-menu-materials-title").ToUpperInvariant();
        _lampLabels =
        [
            Loc.GetString("polonium-lathe-lamp-power").ToUpperInvariant(),
            Loc.GetString("polonium-lathe-lamp-work").ToUpperInvariant(),
            Loc.GetString("polonium-lathe-lamp-silo").ToUpperInvariant(),
        ];
        _lampWidths = new int[_lampLabels.Length];
        for (var i = 0; i < _lampLabels.Length; i++)
        {
            _lampWidths[i] = _font.Measure(_lampLabels[i]) - 1;
        }

        MouseFilter = MouseFilterMode.Stop;
        Resizable = false;
    }

    public LatheTextField? AmountField => _model == null ? null : _amount;

    public LatheCatalog? Catalog => _model == null ? null : _screen.Catalog;

    public IReadOnlyList<LatheCatalogRow> Rows => _model == null ? Array.Empty<LatheCatalogRow>() : _screen.Catalog.Rows;

    private float Unit => PixelCanvas.GetScale(UIScale) / UIScale;

    private int Offset => _tank == null ? 0 : TankGap;

    public void SetEntity(EntityUid uid)
    {
        if (_model != null)
            return;

        var model = new LatheConsoleModel(uid);
        model.Refresh();
        _model = model;
        _icons = new LatheIconCache(_entMan, _clyde);

        _title = _entMan.GetComponent<MetaDataComponent>(uid).EntityName.ToUpperInvariant();

        _screen = new LatheScreen(model, _icons, _screenFont, _textures.Icons);
        _screen.Catalog.OnPressed += Queue;
        AddChild(_screen);

        for (var i = 0; i < _softKeys.Length; i++)
        {
            var index = i;
            _softKeys[i] = AddKey(_textures.KeyLight, LatheKeyIcon.Mark, LathePalette.KeyMark);
            _softKeys[i].OnPressed += _ => _screen.PressSoft(index);
        }

        _rack = new LatheTicketRack(model, _icons, _font, _screenFont, _textures);
        _rack.OnMove += (index, change) => OnMove?.Invoke(index, change);
        _rack.OnDelete += index => OnDelete?.Invoke(index);
        AddChild(_rack);

        _tray = new LatheTray(model, _icons, _font);
        AddChild(_tray);

        _minus = AddKey(_textures.KeyLight, LatheKeyIcon.Minus, LathePalette.KeyText);
        _minus.OnButtonDown += _ => StartRepeat(-1);
        _minus.OnButtonUp += _ => _repeat = 0;
        _plus = AddKey(_textures.KeyLight, LatheKeyIcon.Plus, LathePalette.KeyText);
        _plus.OnButtonDown += _ => StartRepeat(1);
        _plus.OnButtonUp += _ => _repeat = 0;

        _amount = new LatheTextField
        {
            Font = _screenFont,
            TextColor = LathePalette.Screen,
            Placeholder = "1",
            MaxLength = 5,
            DigitsOnly = true,
            Readout = true,
        };
        _amount.OnTextChanged += OnAmountTyped;
        _amount.OnStep += OnAmountArrow;
        AddChild(_amount);

        _clear = AddKey(_textures.KeyLight, LatheKeyIcon.Cross, LathePalette.KeyText);
        _clear.ToolTip = Loc.GetString("polonium-lathe-amount-clear");
        _clear.OnPressed += _ => SetAmount(1);

        _servers = new PixelKey
        {
            Skin = _textures.KeyDark,
            Font = _font,
            Text = Loc.GetString("polonium-lathe-servers").ToUpperInvariant(),
            TextColor = LathePalette.KeyTextDark,
            ToolTip = Loc.GetString("lathe-menu-server-list"),
            Visible = model.HasServers,
        };
        _servers.OnPressed += _ => OnServerList?.Invoke();
        _keySounds.Attach(_servers);
        AddChild(_servers);

        _materials = new LatheMaterials(model, _font, _textures);
        AddChild(_materials);

        _stop = new LatheStopButton(_textures.Stop, _font, Loc.GetString("polonium-lathe-stop").ToUpperInvariant())
        {
            ToolTip = Loc.GetString("polonium-lathe-stop-tooltip"),
        };
        _stop.OnButtonDown += _ =>
        {
            _stopHeld = 0f;
            _stoppedAll = false;
        };
        _stop.OnPressed += _ =>
        {
            if (_stoppedAll)
                return;

            model.NoteAbort();
            OnAbort?.Invoke();
        };
        _keySounds.Attach(_stop, _stopSound);
        AddChild(_stop);

        if (_entMan.TryGetComponent(uid, out GuideHelpComponent? guide) && guide.Guides.Count > 0)
        {
            _help = new PixelKey
            {
                Skin = _textures.KeyLight,
                Font = _font,
                Text = "?",
                TextColor = LathePalette.KeyText,
            };
            _help.OnPressed += _ => _entMan.System<GuidebookSystem>().OpenHelp(guide.Guides);
            _keySounds.Attach(_help);
            AddChild(_help);
        }

        _close = AddKey(_textures.KeyLight, LatheKeyIcon.Cross, LathePalette.KeyText);
        _close.OnPressed += _ => Close();

        if (model.Lube != null)
        {
            _tank = new LatheTank(model, _font, _textures);
            AddChild(_tank);
        }

        var amount = _entMan.TryGetComponent(uid, out LatheComponent? lathe) ? lathe.DefaultProductionAmount : 1;
        SetAmount(Math.Clamp(amount, 1, LatheConsoleModel.MaxAmount));
        InvalidateMeasure();
    }

    private PixelKey AddKey(Texture skin, LatheKeyIcon icon, Color color)
    {
        var key = new PixelKey { Skin = skin };
        key.AddChild(new PixelKeyIcon { Texture = _textures.Icons, Region = LathePalette.Icon(icon), Color = color });
        _keySounds.Attach(key);
        AddChild(key);
        return key;
    }

    public void UpdateState(LatheUpdateState state)
    {
        if (_model == null)
            return;

        _model.Queue = state.Queue;
        _model.SetRecipes(state.Recipes);
    }

    private void Queue(LatheRecipePrototype recipe)
    {
        if (_model!.CanProduce(recipe))
        {
            OnQueue?.Invoke(recipe.ID, _model.Amount);
            return;
        }

        _model.Deny(recipe);
        _keySounds.PlaySignal(_denySound);
    }

    private void SetAmount(int amount)
    {
        _model!.Amount = amount;
        _amount.SetText(amount.ToString());
    }

    private void OnAmountTyped(string text)
    {
        if (!int.TryParse(text, out var amount) || amount <= 0)
        {
            _model!.Amount = 1;
            return;
        }

        if (amount > LatheConsoleModel.MaxAmount)
            SetAmount(LatheConsoleModel.MaxAmount);
        else
            _model!.Amount = amount;
    }

    private void Step(int change)
    {
        SetAmount(Math.Clamp(_model!.Amount + change, 1, LatheConsoleModel.MaxAmount));
    }

    private void OnAmountArrow(int direction)
    {
        Step(direction);
        if (_latched != null)
            _latched.Latched = false;

        _latched = direction > 0 ? _plus : _minus;
        _latched.Latched = true;
        _latchClock = LatchTime;
    }

    private void StartRepeat(int direction)
    {
        Step(direction);
        _repeat = direction;
        _repeats = 1;
        _repeatClock = RepeatDelay;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_model == null || _icons == null)
            return;

        _icons.FrameUpdate();
        _model.Refresh();

        _minus.Disabled = _model.Amount <= 1;
        _plus.Disabled = _model.Amount >= LatheConsoleModel.MaxAmount;
        _servers.Visible = _model.HasServers;

        // A key that disabled itself under the finger never reports its release.
        if (_repeat != 0 && (_repeat < 0 ? _minus : _plus).Disabled)
            _repeat = 0;

        if (_repeat != 0)
        {
            _repeatClock -= args.DeltaSeconds;
            while (_repeatClock <= 0f && !(_repeat < 0 ? _minus : _plus).Disabled)
            {
                Step(_repeats > RepeatsBeforeTens ? _repeat * 10 : _repeat);
                _repeats++;
                _repeatClock += RepeatTime;
                _minus.Disabled = _model.Amount <= 1;
                _plus.Disabled = _model.Amount >= LatheConsoleModel.MaxAmount;
            }
        }

        if (_latched != null && (_latchClock -= args.DeltaSeconds) <= 0f)
        {
            _latched.Latched = false;
            _latched = null;
        }

        if (_stop.DrawMode != BaseButton.DrawModeEnum.Pressed)
        {
            _stopHeld = 0f;
        }
        else if (!_stoppedAll && (_stopHeld += args.DeltaSeconds) >= StopHoldTime)
        {
            _stoppedAll = true;
            _model.NoteAbort();
            _rack.DoomAll();
            _keySounds.PlaySignal(_stopAllSound);
            OnStopAll?.Invoke();
        }

        if (_rack.Batch == _batch)
            return;

        _batch = _rack.Batch;
        _screen.BatchText = _batch is { } batch
            ? Loc.GetString("polonium-lathe-batch", ("index", batch.Index), ("total", batch.Total)).ToUpperInvariant()
            : string.Empty;
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();

        // Covers both the player closing the window and the server closing the interface.
        _icons?.Clear();
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
    {
        return DragMode.Move;
    }

    protected override bool HasPoint(Vector2 point)
    {
        var art = (Vector2i) (point / Unit);
        if (art.X >= Offset)
            return art.X < Offset + ConsoleSize.X && art.Y >= 0 && art.Y < ConsoleSize.Y;

        return art.X >= 0 && art.Y >= TankTop && art.Y < TankTop + LatheTank.ArtHeight;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(availableSize);
        }

        return LatheLayout.Box(UIScale, 0, 0, Offset + ConsoleSize.X, ConsoleSize.Y).Size;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        if (_model == null)
            return finalSize;

        Place(_screen, ScreenArea);
        for (var i = 0; i < _softKeys.Length; i++)
        {
            Place(_softKeys[i], UIBox2i.FromDimensions(SoftLeft[i], SoftTop, SoftWidth[i], SoftHeight));
        }

        Place(_rack, RackArea);
        Place(_tray, TrayArea);
        Place(_minus, MinusKey);
        Place(_amount, AmountArea);
        Place(_plus, PlusKey);
        Place(_clear, ClearKey);
        Place(_servers, ServersKey);
        Place(_materials, MaterialsArea);
        Place(_stop, StopArea);
        Place(_close, CloseKey);
        if (_help != null)
            Place(_help, HelpKey);

        _tank?.Arrange(LatheLayout.Box(UIScale, 0, TankTop, LatheTank.ArtWidth, LatheTank.ArtHeight));

        return finalSize;
    }

    private void Place(Control control, UIBox2i art)
    {
        control.Arrange(LatheLayout.Box(UIScale, art.Left + Offset, art.Top, art.Width, art.Height));
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (_model == null || _icons == null || PixelSize.X <= 0)
            return;

        _icons.Render(handle);

        var canvas = new PixelCanvas(handle, UIScale);
        var left = Offset;
        canvas.Texture(_textures.Body, left, 0);
        canvas.Text(_font, left + QueueLabel.X, QueueLabel.Y, _queueLabel, LathePalette.Engraved);
        canvas.Text(_font, left + AmountLabel.X, AmountLabel.Y, _amountLabel, LathePalette.Engraved);
        canvas.Text(_font, left + MaterialsLabel.X, MaterialsLabel.Y, _materialsLabel, LathePalette.Engraved);

        var lampsLeft = DrawLamps(canvas, left + (_help == null ? CloseKey.Left : HelpKey.Left) - BarGap);
        DrawPlate(canvas, left + PlateLeft, lampsLeft - BarGap);
    }

    private int DrawLamps(PixelCanvas canvas, int right)
    {
        var textTop = BarTop + (BarHeight - LabelLine) / 2;
        var lampTop = textTop + _font.CapTop;
        for (var i = _lampLabels.Length - 1; i >= 0; i--)
        {
            var lit = i switch
            {
                0 => _model!.Powered ? 1 : 0,
                1 => _model!.Printing != null ? 2 : 0,
                _ => _model!.SiloLinked ? 3 : 0,
            };

            right -= _lampWidths[i];
            canvas.Text(_font, right, textTop, _lampLabels[i], LathePalette.Label);
            right -= LampGap + LampSize;
            canvas.Region(_textures.Leds, UIBox2.FromDimensions(lit * LampSize, 0f, LampSize, LampSize), right, lampTop);
            right -= BarGap;
        }

        return right + BarGap;
    }

    private void DrawPlate(PixelCanvas canvas, int left, int right)
    {
        var title = _title.AsSpan();
        title = title[.._plateFont.Fit(title, right - left - PlatePadding * 2)];

        var width = _plateFont.Measure(title) - 1 + PlatePadding * 2;
        canvas.NineSlice(_textures.Plate, PlateFrame, PlateMargin, PlateMargin, PlateMargin, PlateMargin, left, BarTop, width, BarHeight);
        var capTop = BarTop + (BarHeight - _plateFont.CapHeight) / 2;
        canvas.Text(_plateFont, left + PlatePadding, capTop - _plateFont.CapTop, title, LathePalette.PlateText);
    }
}
