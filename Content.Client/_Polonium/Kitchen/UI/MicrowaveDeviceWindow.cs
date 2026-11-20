using System.Numerics;
using Content.Client._Polonium.UserInterface.PixelUi;
using Content.Client.Resources;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Configuration;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Polonium.Kitchen.UI;

public readonly record struct MicrowaveDeviceState(
    IReadOnlyList<EntityUid> Contents,
    int Capacity,
    int TimeIndex,
    bool Active,
    TimeSpan CookEnd,
    float TimeMultiplier,
    bool Powered,
    bool Broken,
    bool Malfunctioning,
    MicrowaveReactions Reactions)
{
    public static readonly MicrowaveDeviceState Off = new(Array.Empty<EntityUid>(), 0, 0, false, TimeSpan.Zero, 1f, false, false, false, default);
}

public sealed partial class MicrowaveDeviceWindow : BaseWindow
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private IRobustRandom _random = default!;

    public const string TexturePath = "/Textures/_Polonium/Interface/Microwave/";
    public const int TimeStep = 5;

    private const string KitPath = "/Textures/_Polonium/Interface/PixelUi/";
    private const int TimeKeyCount = 7;
    private const int TimeKeyColumns = 3;
    private const int LegendTop = 77;
    private const int PlateLeft = 16;
    private const int PlateTop = 7;
    private const int StickerRight = 314;
    private const int StickerMaxWidth = 112;
    private const int StickerMaxLines = 2;
    private const int StickerLineHeight = 7;
    private const int StatusWidth = 74;
    private const int ScrollGap = 3;
    private const float ScrollPause = 0.9f;
    private const float ScrollStep = 0.16f;
    private const float DoorTime = 0.22f;
    private const float CrackTime = 0.35f;
    private const float FumeTime = 7f;
    private const float FumeInterval = 0.05f;
    private const float GrilleFumeChance = 0.5f;
    private const float JoltStep = 0.05f;
    private const float LayoutSlack = 0.25f;
    private const float HoldTime = 2.1f;
    private const float HoldGrace = 0.3f;
    private const int PanelSparks = 10;
    private const int PanelBottom = 204;

    private static readonly TimeSpan DoneTime = TimeSpan.FromSeconds(2.5);
    private static readonly LocId StockPlate = "polonium-microwave-plate";

    private static readonly Vector2i CanvasSize = new(442, 220);
    private static readonly UIBox2i Body = UIBox2i.FromDimensions(6, 0, 330, 218);
    private static readonly UIBox2i Cavity = UIBox2i.FromDimensions(14, 32, 214, 172);
    private static readonly UIBox2i DoorArea = UIBox2i.FromDimensions(0, 32, 228, 172);
    private static readonly UIBox2i Display = UIBox2i.FromDimensions(240, 38, 82, 36);
    private static readonly UIBox2i InstantKey = UIBox2i.FromDimensions(240, 88, 82, 14);
    private static readonly UIBox2i FirstTimeKey = UIBox2i.FromDimensions(240, 104, 26, 14);
    private static readonly Vector2i TimeKeyPitch = new(28, 16);
    private static readonly UIBox2i StartKey = UIBox2i.FromDimensions(240, 138, 82, 19);
    private static readonly UIBox2i EjectKey = UIBox2i.FromDimensions(240, 160, 82, 26);
    private static readonly UIBox2i CloseKey = UIBox2i.FromDimensions(319, 7, 11, 12);
    private static readonly UIBox2i NoteArea = UIBox2i.FromDimensions(342, 22, MicrowaveNote.ArtWidth, MicrowaveNote.ArtHeight);
    private static readonly UIBox2i NotePaper = UIBox2i.FromDimensions(342, 28, MicrowaveNote.ArtWidth, MicrowaveNote.ArtHeight - 6);
    private static readonly UIBox2i Grille = UIBox2i.FromDimensions(244, 188, 74, 10);
    private static readonly Vector2i[] Jolt = [new(-3, 2), new(3, -2), new(-2, -1), new(2, 1), new(-1, 0)];

    private static readonly Color Outline = Color.FromHex("#171717");
    private static readonly Color PlateFill = Color.FromHex("#363636");
    private static readonly Color PlateText = Color.FromHex("#c1bab5");
    private static readonly Color StickerFill = Color.FromHex("#e9c967");
    private static readonly Color StickerText = Color.FromHex("#2b2b2b");
    private static readonly Color Legend = Color.FromHex("#a8a8a8");
    private static readonly Color Lit = Color.FromHex("#30f050");
    private static readonly Color Ghost = Color.FromHex("#042b0b");
    private static readonly Color EjectText = Color.FromHex("#a3261c");
    private static readonly Color[] Fumes = [Color.FromHex("#141210"), Color.FromHex("#24201d"), Color.FromHex("#34302c")];
    private static readonly Color[] Embers = [Color.FromHex("#ff9a2e"), Color.FromHex("#ffd76a")];

    private readonly PixelFont _font;
    private Texture _body;
    private readonly Texture _warning;
    private readonly Texture _segments;
    private readonly MicrowaveChamber _chamber;
    private readonly MicrowaveDoor _door;
    private readonly MicrowaveNote _note;
    private readonly PixelParticles _fumes;
    private readonly List<MicrowaveRow> _rows = new();
    private readonly PixelKey[] _timeKeys = new PixelKey[TimeKeyCount];
    private readonly PixelKey _start;
    private readonly PixelKey _eject;
    private readonly PixelKey _close;
    private readonly List<string> _stickerLines = new();
    private readonly char[] _clock = ['0', ':', '0', '0'];
    private string _title = string.Empty;
    private readonly string _legend;
    private readonly string _emptyStatus;
    private readonly string _cookingStatus;
    private readonly string _doneStatus;
    private int _titleWidth;
    private readonly int _stickerWidth;

    private MicrowaveDeviceState _state = MicrowaveDeviceState.Off;
    private string _count = string.Empty;
    private string _hoverText = string.Empty;
    private EntityUid? _hoveredUid;
    private int _shownCount = -1;
    private int _shownCapacity = -1;
    private float _scroll;
    private float _wreckAge = -1f;
    private float _fumeTimer;
    private float _joltTime = -1f;
    private Vector2 _joltOrigin;
    private bool _applied;
    private bool _cooking;
    private bool _holding;
    private bool _reduced;
    private float _holdAge;
    private TimeSpan _doneUntil;

    public Func<MicrowaveDeviceState>? StateSource;

    public event Action<int>? OnTimeSelected;
    public event Action? OnStart;
    public event Action? OnEjectAll;
    public event Action<EntityUid>? OnEject;

    public MicrowaveDeviceWindow()
    {
        IoCManager.InjectDependencies(this);

        _font = PixelFont.Tiny5(_cache);
        _body = _cache.GetTexture(TexturePath + "body.png");
        _warning = _cache.GetTexture(TexturePath + "warning.png");
        _segments = _cache.GetTexture(KitPath + "segments.png");
        var lightKey = _cache.GetTexture(KitPath + "key_light.png");

        SetPlate(StockPlate);
        _legend = Loc.GetString("polonium-microwave-time-legend");
        _emptyStatus = Loc.GetString("polonium-microwave-status-empty");
        _cookingStatus = Loc.GetString("polonium-microwave-status-cooking");
        _doneStatus = Loc.GetString("polonium-microwave-status-done");
        _stickerWidth = WrapSticker(Loc.GetString("microwave-menu-footer-flavor-left"));

        MouseFilter = MouseFilterMode.Stop;
        Resizable = false;

        _chamber = new MicrowaveChamber();
        _chamber.OnItemPressed += uid => OnEject?.Invoke(uid);
        _chamber.OnHoverChanged += SetHovered;
        AddChild(_chamber);

        _door = new MicrowaveDoor();
        AddChild(_door);

        for (var i = 0; i < TimeKeyCount; i++)
        {
            var index = i;
            var text = i == 0 ? Loc.GetString("microwave-menu-instant-button") : (i * TimeStep).ToString();
            var key = AddKey(lightKey, text);
            key.Lamp = false;
            key.OnPressed += _ => OnTimeSelected?.Invoke(index);
            _timeKeys[i] = key;
        }

        _start = AddKey(_cache.GetTexture(KitPath + "key_green.png"), Loc.GetString("microwave-menu-start-button").ToUpperInvariant());
        _start.TextColor = Color.White;
        _start.OnPressed += _ => PressStart();

        _eject = AddKey(lightKey, Loc.GetString("microwave-menu-eject-all-text").ToUpperInvariant());
        _eject.TextColor = EjectText;
        _eject.ToolTip = Loc.GetString("microwave-menu-eject-all-tooltip");
        _eject.OnPressed += _ => OnEjectAll?.Invoke();

        _close = AddKey(_cache.GetTexture(KitPath + "key_dark.png"), "x");
        _close.TextColor = PlateText;
        _close.MuteSounds = false;
        _close.OnPressed += _ => Close();

        _note = new MicrowaveNote();
        _note.OnItemPressed += uid => OnEject?.Invoke(uid);
        _note.OnHoverChanged += SetHovered;
        AddChild(_note);

        _fumes = new PixelParticles();
        AddChild(_fumes);
    }

    /// <summary>
    /// Gives the window the look of a particular microwave. Anything left out keeps the stock look.
    /// </summary>
    /// <param name="body">Texture of the body.</param>
    /// <param name="light">Texture of the chamber while the microwave runs.</param>
    /// <param name="plate">Text on the name plate.</param>
    public void SetSkin(ResPath? body, ResPath? light, LocId? plate)
    {
        _body = _cache.GetTexture(body ?? new ResPath(TexturePath + "body.png"));
        _chamber.SetLight(light);
        SetPlate(plate ?? StockPlate);
    }

    private void SetPlate(LocId plate)
    {
        _title = Loc.GetString(plate,
            ("name", Loc.GetString("microwave-menu-title").ToUpperInvariant()),
            ("version", Loc.GetString("microwave-menu-footer-flavor-right")));
        _titleWidth = _font.Measure(_title) - 1;
    }

    private PixelKey AddKey(Texture skin, string text)
    {
        var key = new PixelKey
        {
            Skin = skin,
            Font = _font,
            Text = text,
            MuteSounds = true,
        };
        AddChild(key);
        return key;
    }

    private int WrapSticker(string text)
    {
        var line = string.Empty;
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var longer = line.Length == 0 ? word : $"{line} {word}";
            if (line.Length > 0 && _font.Measure(longer) - 1 > StickerMaxWidth)
            {
                _stickerLines.Add(line);
                line = word;
            }
            else
            {
                line = longer;
            }
        }

        if (line.Length > 0)
            _stickerLines.Add(line);

        if (_stickerLines.Count > StickerMaxLines)
            _stickerLines.RemoveRange(StickerMaxLines, _stickerLines.Count - StickerMaxLines);

        var width = 0;
        foreach (var wrapped in _stickerLines)
        {
            width = Math.Max(width, _font.Measure(wrapped) - 1);
        }

        return width;
    }

    private void SetHovered(EntityUid? uid)
    {
        var text = uid != null && _entMan.TryGetComponent(uid.Value, out MetaDataComponent? meta)
            ? meta.EntityName.ToUpperInvariant()
            : string.Empty;

        if (_hoveredUid != uid || _hoverText != text)
            _scroll = 0f;

        _hoveredUid = uid;
        _chamber.Highlighted = uid;
        _note.Highlighted = uid;
        _hoverText = text;
    }

    /// <summary>
    /// An instant cook is over within a tick, so the window keeps the door shut for a moment to show what it did to the contents.
    /// </summary>
    private void PressStart()
    {
        var watched = _state.TimeIndex == 0 && !_cooking && !_cfg.GetCVar(CCVars.ReducedMotion) && _chamber.HasStartReaction();
        OnStart?.Invoke();
        if (!watched || StateSource?.Invoke().Active != true)
            return;

        _holding = true;
        _holdAge = 0f;
        _chamber.Hold();
    }

    private void Apply(in MicrowaveDeviceState state)
    {
        var opening = !_applied;
        _applied = true;

        if (state.Broken && _wreckAge < 0f)
            Wreck(opening);

        var wrecked = _wreckAge >= 0f;
        var now = _timing.RealTime;
        if (_holding && (wrecked || _chamber.MeltingComplete && (_holdAge >= HoldTime || _holdAge >= HoldGrace && !state.Active && state.Contents.Count > 0)))
        {
            _holding = false;
            _chamber.Release();
        }

        var cooking = !wrecked && (state.Active || _holding);
        if (cooking)
            _doneUntil = TimeSpan.Zero;
        else if (_cooking && !wrecked && state.Contents.Count == 0)
            _doneUntil = now + DoneTime;

        if (opening && cooking)
            _door.Open = 0f;

        _cooking = cooking;
        _state = state;

        var on = state.Powered && !state.Broken;
        var idle = !cooking && !wrecked;
        var loaded = state.Contents.Count > 0;

        _chamber.Reactions = state.Reactions;
        _chamber.SetCooking(cooking, opening);
        _chamber.SetContents(state.Contents);
        _chamber.CopyRows(_rows);
        SetHovered(_hoveredUid);
        _chamber.Lit = cooking;
        _chamber.Malfunctioning = state.Malfunctioning;
        _chamber.Interactive = idle;

        if (_rows.Count != _shownCount || state.Capacity != _shownCapacity)
        {
            _shownCount = _rows.Count;
            _shownCapacity = state.Capacity;
            _count = $"{_rows.Count}/{state.Capacity}";
        }

        _note.SetRows(_rows, state.Capacity);
        _note.Interactive = idle;

        for (var i = 0; i < _timeKeys.Length; i++)
        {
            _timeKeys[i].Disabled = !idle || !on;
            _timeKeys[i].Lamp = on && i == state.TimeIndex;
        }

        _start.Disabled = !idle || !on || !loaded;
        _start.Latched = cooking;
        _eject.Disabled = !idle || !loaded;
    }

    /// <param name="settled">The microwave was already broken when the window opened.</param>
    private void Wreck(bool settled)
    {
        var quiet = settled || _cfg.GetCVar(CCVars.ReducedMotion);
        _wreckAge = quiet ? FumeTime : 0f;
        _chamber.Wreck(quiet);
        _door.Open = 0f;
        if (quiet)
            return;

        _joltTime = 0f;
        _joltOrigin = Position;
        for (var i = 0; i < PanelSparks; i++)
        {
            _fumes.Add(
                new Vector2(_random.NextFloat(Grille.Left, Grille.Right), PanelBottom),
                new Vector2(_random.NextFloat(-20f, 20f), _random.NextFloat(10f, 50f)),
                _random.NextFloat(0.3f, 0.6f),
                1,
                _random.Pick(Embers),
                gravity: 160f);
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var reduced = _cfg.GetCVar(CCVars.ReducedMotion);
        _reduced = reduced;
        _chamber.ReducedMotion = reduced;
        _note.ReducedMotion = reduced;

        _holdAge += args.DeltaSeconds;
        if (StateSource != null)
            Apply(StateSource());

        _scroll += args.DeltaSeconds;
        if (_wreckAge >= 0f)
        {
            UpdateWreck(args.DeltaSeconds, reduced);
        }
        else
        {
            var target = _cooking ? 0f : 1f;
            var step = args.DeltaSeconds / DoorTime;
            if (reduced || MathF.Abs(target - _door.Open) <= step)
                _door.Open = target;
            else
                _door.Open += MathF.Sign(target - _door.Open) * step;
        }

        _chamber.Sealed = _door.Open <= 0f;
    }

    private void UpdateWreck(float delta, bool reduced)
    {
        _wreckAge += delta;
        _door.Open = 0f;
        _door.Cracks = reduced ? 1f : MathF.Min(1f, _wreckAge / CrackTime);

        if (_joltTime >= 0f)
        {
            _joltTime += delta;
            var step = (int) (_joltTime / JoltStep);
            var done = reduced || step >= Jolt.Length;
            LayoutContainer.SetPosition(this, done ? _joltOrigin : _joltOrigin + (Vector2) Jolt[step] * Unit);
            if (done)
                _joltTime = -1f;
        }

        _fumeTimer -= delta;
        if (reduced || _wreckAge >= FumeTime || _fumeTimer > 0f)
            return;

        _fumeTimer = FumeInterval;
        AddFume(_random.NextFloat(Cavity.Left + 4f, Cavity.Right - 6f), Cavity.Top + _random.NextFloat(-2f, 3f), _random.NextFloat(0.9f, 1.7f), _random.Next(2, 5));
        if (_random.Prob(GrilleFumeChance))
            AddFume(_random.NextFloat(Grille.Left, Grille.Right), _random.NextFloat(Grille.Top, Grille.Bottom), _random.NextFloat(0.8f, 1.5f), _random.Next(2, 4));
    }

    private void AddFume(float x, float y, float life, int size)
    {
        _fumes.Add(
            new Vector2(x, y),
            new Vector2(_random.NextFloat(-5f, 5f), _random.NextFloat(-26f, -12f)),
            life,
            size,
            _random.Pick(Fumes),
            gravity: -6f,
            drag: 0.6f,
            fade: true);
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
    {
        return DragMode.Move;
    }

    protected override bool HasPoint(Vector2 point)
    {
        var art = (Vector2i) (point / Unit);
        return Body.Contains(art)
               || NotePaper.Contains(art)
               || art.X < MicrowaveDoor.OpenWidth && art.Y >= DoorArea.Top && art.Y < DoorArea.Bottom && _door.Open > 0f;
    }

    private float Unit => PixelCanvas.GetScale(UIScale) / UIScale;

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(availableSize);
        }

        return new Vector2(Snap(CanvasSize.X), Snap(CanvasSize.Y));
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        Place(_chamber, Cavity);
        Place(_door, DoorArea);
        Place(_timeKeys[0], InstantKey);
        for (var i = 1; i < _timeKeys.Length; i++)
        {
            var cell = new Vector2i((i - 1) % TimeKeyColumns, (i - 1) / TimeKeyColumns);
            Place(_timeKeys[i], FirstTimeKey.Translated(cell * TimeKeyPitch));
        }

        Place(_start, StartKey);
        Place(_eject, EjectKey);
        Place(_close, CloseKey);
        Place(_note, NoteArea);
        Place(_fumes, UIBox2i.FromDimensions(Vector2i.Zero, CanvasSize));

        return finalSize;
    }

    private void Place(Control control, UIBox2i art)
    {
        control.Arrange(UIBox2.FromDimensions(Snap(art.Left), Snap(art.Top), Snap(art.Width), Snap(art.Height)));
    }

    // The engine cuts positions and sizes down to whole screen pixels, so without the slack
    // float error costs a row or a column of art at interface scales such as 125 %.
    private float Snap(int art)
    {
        return (art * PixelCanvas.GetScale(UIScale) + LayoutSlack) / UIScale;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var canvas = new PixelCanvas(handle, UIScale);
        canvas.Texture(_body, Body.Left, Body.Top);

        canvas.Rect(PlateLeft, PlateTop, _titleWidth + 10, 13, Outline);
        canvas.Rect(PlateLeft + 1, PlateTop + 1, _titleWidth + 8, 11, PlateFill);
        canvas.Text(_font, PlateLeft + 5, PlateTop + 2, _title, PlateText);

        DrawSticker(canvas);
        canvas.Text(_font, Display.Left, LegendTop, _legend, Legend);

        if (_state.Powered && !_state.Broken)
            DrawDisplay(canvas);
    }

    private void DrawSticker(PixelCanvas canvas)
    {
        if (_stickerLines.Count == 0)
            return;

        var width = _stickerWidth + 18;
        var height = _stickerLines.Count * StickerLineHeight + 5;
        var left = StickerRight - width;
        var top = (Body.Top + 26 - height) / 2;
        canvas.Rect(left, top, width, height, Outline);
        canvas.Rect(left + 1, top + 1, width - 2, height - 2, StickerFill);
        canvas.Texture(_warning, left + 3, top + (height - _warning.Height) / 2);
        for (var i = 0; i < _stickerLines.Count; i++)
        {
            canvas.Text(_font, left + 14, top + 1 + i * StickerLineHeight, _stickerLines[i], StickerText);
        }
    }

    private void DrawDisplay(PixelCanvas canvas)
    {
        var seconds = _state.TimeIndex * TimeStep;
        if (_state.Active && !_holding)
        {
            var left = (_state.CookEnd - _timing.CurTime).TotalSeconds;
            if (_state.TimeMultiplier > 0f)
                left /= _state.TimeMultiplier;

            seconds = (int) Math.Clamp(Math.Ceiling(left), 0d, 599d);
        }

        _clock[0] = (char) ('0' + seconds / 60);
        _clock[2] = (char) ('0' + seconds % 60 / 10);
        _clock[3] = (char) ('0' + seconds % 10);
        canvas.Segments(_segments, Display.Left + 4, Display.Top + 4, _clock, Lit, Ghost);
        canvas.Text(_font, Display.Right - 4 - _font.Measure(_count) + 1, Display.Top + 5, _count, Lit);
        var status = Status();
        var shown = status.AsSpan(ScrollStart(status));
        canvas.Text(_font, Display.Left + 4, Display.Top + 23, shown[.._font.Fit(shown, StatusWidth)], Lit);
    }

    private string Status()
    {
        if (_cooking)
            return _cookingStatus;

        if (_hoverText.Length > 0)
            return _hoverText;

        if (_timing.RealTime < _doneUntil)
            return _doneStatus;

        return _rows.Count == 0 ? _emptyStatus : string.Empty;
    }

    private int ScrollStart(string text)
    {
        if (_reduced || _font.Fit(text, StatusWidth) >= text.Length)
            return 0;

        var steps = (int) MathF.Max(0f, (_scroll - ScrollPause) / ScrollStep);
        return Math.Min(steps % (text.Length + ScrollGap), text.Length);
    }
}
