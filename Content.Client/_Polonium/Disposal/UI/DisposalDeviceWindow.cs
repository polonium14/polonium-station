using System.Numerics;
using Content.Client._Polonium.Kitchen.UI;
using Content.Client._Polonium.UserInterface.PixelUi;
using Content.Client.Popups;
using Content.Client.Resources;
using Content.Shared.CCVar;
using Content.Shared.Disposal.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Disposal.UI;

/// <summary>
/// Where the parts common to both units stand, in the coordinates of the unit's body.
/// </summary>
/// <param name="Body">File name of the body texture.</param>
/// <param name="Size">Size of the window in art pixels.</param>
/// <param name="Top">Height of the transparent strip above the body.</param>
/// <param name="Mouth">The opening things go through.</param>
/// <param name="LeverArea">The area the player grabs the lever by.</param>
public readonly record struct DisposalDeviceLayout(
    string Body,
    Vector2i Size,
    int Top,
    UIBox2i Mouth,
    UIBox2i LeverArea,
    int KeyRow,
    int PowerX,
    int EjectX,
    int SlipX,
    int SlipWidth,
    int CloseX,
    int CloseWidth);

/// <summary>
/// A disposal or mailing unit drawn as the machine itself: a lever to pull, a mouth to reach into, lamps and keys on the plinth.
/// </summary>
public abstract partial class DisposalDeviceWindow : BaseWindow
{
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IResourceCache _cache = default!;

    private const int KeyHeight = 11;
    private const int PowerWidth = 14;
    private const int EjectWidth = 13;
    private const int MobScale = 2;
    private const int MobHop = 8;
    private const int MaxLeaving = 6;
    private const int HopDepth = 3;
    private const float HopDown = 0.05f;
    private const float HopBack = 0.23f;
    private const float HopLate = 0.5f;
    private const float ClimbTime = 0.52f;
    private const float DropTime = 0.18f;
    private const float DropGap = 0.07f;
    private const float MeasureLimit = 0.2f;
    private const float LeverDownTime = 0.35f;
    private const float LeverUpTime = 0.3f;
    private const float DragThreshold = 1.5f;
    private const float FlushQuiet = 1f;
    private const float InsertKick = 0.3f;
    private const float TakeKick = 0.5f;
    private const float GhostFade = 0.45f;
    private const int MaxTexts = 128;

    /// <summary>
    /// Both blinking lamps change between the two frames of the sprite's blue lamp at this pace.
    /// </summary>
    private const float BlinkTime = 0.2f;

    private static readonly ProtoId<SoundCollectionPrototype> LeverSounds = "PixelKeyDown";

    private readonly List<(Control Control, UIBox2i Art)> _layout = new();
    private readonly List<DisposalDeviceLayer> _layers = new();
    private readonly List<Drop> _drops = new();
    private readonly List<Climb> _climbs = new();
    private readonly Dictionary<(string Text, int Width), string> _fitted = new();
    private readonly Dictionary<(string Key, string Value), string> _texts = new();
    private readonly SoundSpecifier _leverSound = new SoundCollectionSpecifier(LeverSounds);
    private readonly GrinderSpriteProbe _probe;
    private readonly PixelKeySoundUIController _keySounds;
    private readonly Texture _body;
    private readonly DisposalDeviceKey _power;
    private readonly DisposalDeviceKey _eject;
    private readonly DisposalDeviceKey _close;
    private readonly DisposalDeviceKey _mouth;
    private readonly Tooltip _statusTip = new();
    private readonly string _ellipsis = "…";

    private HashSet<EntityUid> _known = new();
    private HashSet<EntityUid> _present = new();
    private float _pullFrom;
    private float _hopTime = float.MaxValue;
    private float _blink;
    private int _hop;
    private DisposalsPressureState _tipState;
    private bool _tipPowered;
    private int _tipPercent = -1;
    private int _hoppedFor;
    private int _seenFlush;
    private int _nextClimbId = 1;
    private int _climbSide = 1;
    private bool _tracking;
    private bool _pullMoved;
    private bool _raisedByPlayer;
    private bool _wasEngaged;

    protected readonly DisposalDeviceLayout Layout;
    protected readonly PixelFont Font;
    protected readonly DisposalDeviceGrip Grip;
    protected readonly DisposalDeviceKey Slip;
    protected readonly string NoPowerText;
    protected readonly string ReadyText;
    protected readonly string ChargingText;
    protected DisposalDeviceModel? Model;
    protected float LeverPosition;
    protected float Kick;
    protected bool ReducedMotion;

    /// <summary>
    /// Raised by the power key, the eject key and the lever.
    /// </summary>
    public event Action<DisposalUnitUiButton>? OnButton;

    /// <summary>
    /// Raised by a click on the mouth. The server decides from the player's hand what it means.
    /// </summary>
    public event Action? OnMouth;

    /// <summary>
    /// The lamp frame of a blink: false for the bright one and true for the dim one.
    /// </summary>
    protected bool BlinkDim => !ReducedMotion && (int) (_blink / BlinkTime) % 2 == 1;

    /// <summary>
    /// Whether creatures are seen dropping in. Only the disposal unit is wide enough for it.
    /// </summary>
    protected virtual bool ShowsCreatures => false;

    protected DisposalDeviceWindow(DisposalDeviceLayout layout)
    {
        IoCManager.InjectDependencies(this);

        Layout = layout;
        Font = PixelFont.Tiny5(_cache);
        _body = _cache.GetTexture(DisposalDeviceArt.TexturePath + layout.Body);
        _keySounds = UserInterfaceManager.GetUIController<PixelKeySoundUIController>();
        _probe = new GrinderSpriteProbe(_entMan, _clyde);

        NoPowerText = Loc.GetString("polonium-disposal-no-power");
        ReadyText = Loc.GetString("polonium-disposal-ready");
        ChargingText = Loc.GetString("polonium-disposal-charging");

        MouseFilter = MouseFilterMode.Stop;
        Resizable = false;

        _mouth = new DisposalDeviceKey { MuteSounds = true };
        _mouth.OnPressed += _ => OnMouth?.Invoke();
        AddPart(_mouth, layout.Mouth);

        _power = AddKey(UIBox2i.FromDimensions(layout.PowerX, layout.KeyRow, PowerWidth, KeyHeight), Loc.GetString("polonium-disposal-power"));
        _power.OnPressed += _ => OnButton?.Invoke(DisposalUnitUiButton.Power);

        _eject = AddKey(UIBox2i.FromDimensions(layout.EjectX, layout.KeyRow, EjectWidth, KeyHeight), Loc.GetString("polonium-disposal-eject"));
        _eject.OnPressed += _ => OnButton?.Invoke(DisposalUnitUiButton.Eject);

        Slip = AddKey(UIBox2i.FromDimensions(layout.SlipX, layout.KeyRow, layout.SlipWidth, KeyHeight), null);

        _close = AddKey(UIBox2i.FromDimensions(layout.CloseX, layout.KeyRow, layout.CloseWidth, KeyHeight), Loc.GetString("polonium-disposal-close"));
        _close.OnPressed += _ => Close();

        Grip = new DisposalDeviceGrip();
        Grip.OnGrab += GrabLever;
        Grip.OnDrag += DragLever;
        Grip.OnRelease += ReleaseLever;
        AddPart(Grip, layout.LeverArea);

        AddLayer(layout.Mouth, DrawDrops);
    }

    /// <summary>
    /// Tells the window which unit it shows.
    /// </summary>
    public void SetEntity(EntityUid unit)
    {
        Model = new DisposalDeviceModel(unit, _entMan, _timing, _player);
        Model.Refresh();
        LeverPosition = Model.Engaged ? 1f : 0f;
        _wasEngaged = Model.Engaged;
        _hoppedFor = Model.FlushCount;
        _seenFlush = Model.FlushCount;
        OnModelSet(Model);
    }

    /// <summary>
    /// Called once when the window learns which unit it shows.
    /// </summary>
    protected virtual void OnModelSet(DisposalDeviceModel model)
    {
    }

    /// <summary>
    /// Adds a control over a part of the picture, placed in the coordinates of the body.
    /// </summary>
    protected void AddPart(Control control, UIBox2i art)
    {
        AddChild(control);
        _layout.Add((control, art.Translated(new Vector2i(0, Layout.Top))));
    }

    /// <summary>
    /// Adds a key with the sound of a real switch.
    /// </summary>
    protected DisposalDeviceKey AddKey(UIBox2i art, string? toolTip)
    {
        var key = new DisposalDeviceKey { ToolTip = toolTip };
        _keySounds.Attach(key);
        AddPart(key, art);
        return key;
    }

    /// <summary>
    /// Adds the area of the pressure gauge. Its tooltip goes on following the pressure while it is up.
    /// </summary>
    protected void AddStatusArea(UIBox2i art)
    {
        AddPart(new Control { MouseFilter = MouseFilterMode.Pass, TooltipSupplier = _ => _statusTip }, art);
    }

    /// <summary>
    /// Adds a part drawn over the body and cut off at the given area, which is in the coordinates of the body.
    /// </summary>
    protected void AddLayer(UIBox2i art, Action<DisposalDeviceBrush> paint)
    {
        var inWindow = art.Translated(new Vector2i(0, Layout.Top));
        var layer = new DisposalDeviceLayer(inWindow, Layout.Top, (canvas, x, y) => paint(new DisposalDeviceBrush(canvas, Font, x, y)));
        AddChild(layer);
        _layers.Add(layer);
    }

    /// <summary>
    /// Text in capitals, shortened with an ellipsis so that it fits the given width.
    /// </summary>
    protected string Fit(string text, int width)
    {
        if (_fitted.TryGetValue((text, width), out var fitted))
            return fitted;

        var upper = text.ToUpperInvariant();
        if (Font.Measure(upper) - 1 <= width)
        {
            fitted = upper;
        }
        else
        {
            var count = Font.Fit(upper, width - Font.Measure(_ellipsis));
            fitted = upper.Substring(0, count).TrimEnd() + _ellipsis;
        }

        _fitted[(text, width)] = fitted;
        return fitted;
    }

    /// <summary>
    /// A localized text with one value in it, kept so that a tooltip read every frame does not format it every frame.
    /// </summary>
    protected string Text(string key, string argument, string value)
    {
        if (_texts.TryGetValue((key, value), out var text))
            return text;

        if (_texts.Count >= MaxTexts)
            _texts.Clear();

        text = Loc.GetString(key, (argument, value));
        _texts[(key, value)] = text;
        return text;
    }

    /// <summary>
    /// Shows the game's own popup at the cursor.
    /// </summary>
    protected void Popup(string text)
    {
        _entMan.System<PopupSystem>().PopupCursor(text);
    }

    /// <summary>
    /// How far down the lever is (0 to 1) with the pointer the given number of art pixels from where it was grabbed.
    /// </summary>
    protected abstract float LeverAt(float from, float delta);

    /// <summary>
    /// Whether a point in the coordinates of the body lies on the unit.
    /// </summary>
    protected abstract bool OnBody(Vector2i point);

    /// <summary>
    /// Draws what is specific to this unit on top of its body.
    /// </summary>
    protected abstract void DrawUnit(DisposalDeviceBrush brush, DisposalDeviceModel model);

    /// <summary>
    /// Refreshes the controls and tooltips specific to this unit.
    /// </summary>
    protected virtual void UpdateUnit(DisposalDeviceModel model, float frameTime)
    {
    }

    /// <summary>
    /// Called when the unit let go of an engaged lever by itself, without a flush.
    /// </summary>
    protected virtual void OnLeverLetGo(DisposalDeviceModel model)
    {
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (Model is not { } model)
            return;

        var dt = args.DeltaSeconds;
        ReducedMotion = _cfg.GetCVar(CCVars.ReducedMotion);
        model.Refresh();

        _blink = (_blink + dt) % (BlinkTime * 2f);
        Kick = MathF.Max(0f, Kick - dt);

        UpdateFlush(model, dt);
        UpdateLever(model, dt);
        TrackContents(model);
        AdvanceAnimations(dt);
        UpdateMouthTip(model);
        UpdateStatusTip(model);
        UpdateUnit(model, dt);

        var hop = Hop();
        if (hop != _hop)
        {
            _hop = hop;
            InvalidateArrange();
        }
    }

    private void UpdateFlush(DisposalDeviceModel model, float dt)
    {
        _hopTime = _hopTime == float.MaxValue ? _hopTime : _hopTime + dt;
        if (_seenFlush != model.FlushCount)
        {
            _seenFlush = model.FlushCount;
            Grip.Drop();
        }

        if (_hoppedFor == model.FlushCount || model.SinceFlush < SlamTime)
            return;

        _hoppedFor = model.FlushCount;
        if (!ReducedMotion && model.SinceFlush < SlamTime + HopLate)
            _hopTime = 0f;
    }

    /// <summary>
    /// Seconds from the flush to the moment the hatch slams shut and the body jumps.
    /// </summary>
    protected abstract float SlamTime { get; }

    private void UpdateLever(DisposalDeviceModel model, float dt)
    {
        if (_wasEngaged && !model.Engaged)
        {
            if (!_raisedByPlayer && model.SinceFlush > FlushQuiet)
                OnLeverLetGo(model);

            _raisedByPlayer = false;
            Grip.Drop();
        }

        _wasEngaged = model.Engaged;

        if (Grip.Grabbed)
            return;

        var target = model.Engaged ? 1f : 0f;
        LeverPosition = ReducedMotion
            ? target
            : LeverPosition + Math.Clamp(target - LeverPosition, -dt / LeverUpTime, dt / LeverDownTime);
    }

    private void GrabLever()
    {
        _pullFrom = DisposalDeviceArt.Ease(LeverPosition);
        _pullMoved = false;
    }

    private void DragLever(float delta)
    {
        if (MathF.Abs(delta) >= DragThreshold)
            _pullMoved = true;

        if (_pullMoved)
            LeverPosition = DisposalDeviceArt.Unease(Math.Clamp(LeverAt(_pullFrom, delta), 0f, 1f));
    }

    private void ReleaseLever()
    {
        if (Model is not { } model)
            return;

        if (_pullMoved && DisposalDeviceArt.Ease(LeverPosition) > 0.5f == model.Engaged)
            return;

        _raisedByPlayer = model.Engaged;
        _keySounds.PlaySignal(_leverSound);
        OnButton?.Invoke(DisposalUnitUiButton.Engage);
    }

    private void TrackContents(DisposalDeviceModel model)
    {
        var contents = model.Contents;
        var quiet = !_tracking || ReducedMotion || model.SinceFlush < FlushQuiet;

        _present.Clear();
        for (var i = 0; i < contents.Count; i++)
        {
            var uid = contents[i];
            _present.Add(uid);
            if (!quiet && !_known.Contains(uid))
                Enter(model, uid);
        }

        var leaving = 0;
        foreach (var uid in _known)
        {
            if (_present.Contains(uid))
                continue;

            ForgetClimb(uid);
            if (!quiet && leaving < MaxLeaving && Leave(model, uid, leaving))
                leaving++;
        }

        (_known, _present) = (_present, _known);
        _tracking = true;
    }

    private void Enter(DisposalDeviceModel model, EntityUid uid)
    {
        if (ShowsCreatures && model.IsSmallCreature(uid))
        {
            var wait = 0f;
            foreach (var climb in _climbs)
            {
                wait = MathF.Max(wait, ClimbTime - climb.Time);
            }

            _climbSide = -_climbSide;
            _probe.Get(uid, Facing(_climbSide));
            _climbs.Add(new Climb(uid, -wait, _climbSide, _nextClimbId++));
            return;
        }

        if (!model.IsItem(uid))
            return;

        _probe.Get(uid);
        _drops.Add(new Drop(uid, 0f, 1, 1));
        Kick = InsertKick;
    }

    private bool Leave(DisposalDeviceModel model, EntityUid uid, int order)
    {
        var creature = ShowsCreatures && model.IsSmallCreature(uid);
        if (!creature && !model.IsItem(uid))
            return false;

        _probe.Get(uid);
        _drops.Add(new Drop(uid, -DropGap * order, -1, creature ? MobScale : 1));
        Kick = TakeKick;
        return true;
    }

    private void ForgetClimb(EntityUid uid)
    {
        for (var i = _climbs.Count - 1; i >= 0; i--)
        {
            if (_climbs[i].Uid == uid)
                _climbs.RemoveAt(i);
        }
    }

    private void AdvanceAnimations(float dt)
    {
        for (var i = _drops.Count - 1; i >= 0; i--)
        {
            var drop = _drops[i];
            if (drop.Time >= 0f && Measuring(drop.Uid, Direction.South, ref drop.Waited, dt))
                continue;

            drop.Time += dt;
            if (drop.Time > DropTime)
                _drops.RemoveAt(i);
        }

        for (var i = _climbs.Count - 1; i >= 0; i--)
        {
            var climb = _climbs[i];
            if (climb.Time >= 0f && Measuring(climb.Uid, Facing(climb.Side), ref climb.Waited, dt))
                continue;

            climb.Time += dt;
            if (climb.Time >= ClimbTime)
                _climbs.RemoveAt(i);
        }
    }

    /// <summary>
    /// Holds an animation back until the size of its picture is known, because the picture is placed by its visible pixels.
    /// </summary>
    private bool Measuring(EntityUid uid, Direction facing, ref float waited, float dt)
    {
        if (waited >= MeasureLimit || _probe.Get(uid, facing).Palette.Length > 0)
            return false;

        waited += dt;
        return true;
    }

    private bool Measured(EntityUid uid, Direction facing, float waited)
    {
        return waited >= MeasureLimit || _probe.Get(uid, facing).Palette.Length > 0;
    }

    private static Direction Facing(int side)
    {
        return side > 0 ? Direction.East : Direction.West;
    }

    private void UpdateMouthTip(DisposalDeviceModel model)
    {
        string? tip = null;
        if (model.Held is { } held)
        {
            tip = Text("polonium-disposal-put", "name", model.Name(held));
        }
        else if (!model.HandBusy && model.Contents.Count > 0)
        {
            var last = model.Contents[model.Contents.Count - 1];
            if (model.IsItem(last))
                tip = Text("polonium-disposal-take", "name", model.Name(last));
        }

        _mouth.SetLiveToolTip(tip);
    }

    private void UpdateStatusTip(DisposalDeviceModel model)
    {
        var percent = (int) MathF.Round(model.Pressure * 100f);
        if (percent == _tipPercent && model.State == _tipState && model.Powered == _tipPowered)
            return;

        _tipPercent = percent;
        _tipState = model.State;
        _tipPowered = model.Powered;
        var state = model.State == DisposalsPressureState.Ready ? ReadyText : ChargingText;
        _statusTip.Text = model.Powered
            ? Loc.GetString("polonium-disposal-status", ("state", state), ("percent", percent))
            : NoPowerText;
    }

    private int Hop()
    {
        var time = _hopTime;
        if (time >= HopDown + HopBack)
            return 0;

        return time < HopDown
            ? (int) MathF.Ceiling(HopDepth * time / HopDown)
            : (int) MathF.Round(HopDepth * (1f - DisposalDeviceArt.Ease((time - HopDown) / HopBack)));
    }

    private bool TrySprite(EntityUid uid, out SpriteComponent? sprite)
    {
        sprite = null;
        return !_entMan.Deleted(uid) && _entMan.TryGetComponent(uid, out sprite);
    }

    /// <summary>
    /// Things passing through the mouth, and a faint picture of the held item while the pointer rests on it.
    /// </summary>
    private void DrawDrops(DisposalDeviceBrush brush)
    {
        var mouth = Layout.Mouth;
        var centerX = mouth.Left + mouth.Width / 2;
        foreach (var drop in _drops)
        {
            if (drop.Time < 0f || !Measured(drop.Uid, Direction.South, drop.Waited) || !TrySprite(drop.Uid, out var sprite))
                continue;

            var info = _probe.Get(drop.Uid);
            var progress = Math.Min(1f, drop.Time / DropTime);
            var height = (info.Bottom - info.Top + 1) * drop.Scale;
            var rest = mouth.Top + mouth.Height / 2f + height / 2f;
            var far = (mouth.Height + height) / 1.4f;
            var feet = drop.Direction > 0 ? rest + progress * far : rest + (1f - progress) * far;
            brush.Entity(drop.Uid, sprite!, centerX - info.CenterX * drop.Scale, (int) MathF.Round(feet) - (info.Bottom + 1) * drop.Scale, drop.Scale);
        }

        if (!_mouth.Hovered || _drops.Count > 0 || Model?.Held is not { } held || !TrySprite(held, out var heldSprite))
            return;

        var heldInfo = _probe.Get(held);
        var heldHeight = heldInfo.Bottom - heldInfo.Top + 1;
        var heldWidth = heldInfo.Right - heldInfo.Left + 1;
        var heldFeet = mouth.Top + (mouth.Height + heldHeight) / 2;
        brush.Entity(held, heldSprite!, centerX - heldInfo.CenterX, heldFeet - (heldInfo.Bottom + 1), 1);
        brush.Rect(centerX - heldWidth / 2, heldFeet - heldHeight, heldWidth, heldHeight, DisposalDeviceArt.Mouth.WithAlpha(GhostFade));
    }

    /// <summary>
    /// Creatures appear right above the mouth, give a small hop and fall in behind its front lip.
    /// </summary>
    protected void DrawClimbs(DisposalDeviceBrush brush)
    {
        var mouth = Layout.Mouth;
        var lip = mouth.Top + mouth.Height;
        foreach (var climb in _climbs)
        {
            var facing = Facing(climb.Side);
            if (climb.Time < 0f || !Measured(climb.Uid, facing, climb.Waited) || !TrySprite(climb.Uid, out var sprite))
                continue;

            var info = _probe.Get(climb.Uid, facing);
            var height = (info.Bottom - info.Top + 1) * MobScale;
            var progress = Math.Min(1f, climb.Time / ClimbTime);
            float start = Math.Max(mouth.Top + 6, height - 16);
            float end = lip + height + 2;
            var arc = DisposalDeviceArt.ArcHeight(start, start - MobHop, end);
            var feet = start + (end - start) * progress - 4f * arc * progress * (1f - progress);
            var x = mouth.Left + mouth.Width / 2f + (climb.Id * 37) % 13 - 6f - climb.Side * 8f * (1f - progress);
            brush.Entity(climb.Uid, sprite!, (int) MathF.Round(x) - info.CenterX * MobScale, (int) MathF.Round(feet) - (info.Bottom + 1) * MobScale, MobScale, facing);
        }
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
    {
        return DragMode.Move;
    }

    protected override bool HasPoint(Vector2 point)
    {
        var art = (Vector2i) (point * UIScale / PixelCanvas.GetScale(UIScale));
        return OnBody(new Vector2i(art.X, art.Y - Layout.Top));
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(availableSize);
        }

        return PixelLayout.Box(UIScale, UIBox2i.FromDimensions(Vector2i.Zero, Layout.Size)).Size;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        foreach (var (control, art) in _layout)
        {
            control.Arrange(PixelLayout.Box(UIScale, art));
        }

        foreach (var layer in _layers)
        {
            layer.Arrange(PixelLayout.Box(UIScale, layer.Art.Translated(new Vector2i(0, _hop))));
        }

        return finalSize;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (Model is not { } model || PixelSize.X <= 0 || PixelSize.Y <= 0)
            return;

        _probe.Render(handle);

        var canvas = new PixelCanvas(handle, UIScale);
        var top = Layout.Top + _hop;
        canvas.Texture(_body, 0, top);
        var brush = new DisposalDeviceBrush(canvas, Font, 0, top);
        DrawUnit(brush, model);
        DrawKeys(brush, model);
    }

    /// <summary>
    /// The keys on the plinth. They stay lit without power, because they are switches and not screens.
    /// </summary>
    private void DrawKeys(DisposalDeviceBrush brush, DisposalDeviceModel model)
    {
        var y = Layout.KeyRow;
        var x = Layout.PowerX;
        var on = model.SwitchedOn;
        var left = on ? DisposalDeviceArt.RockerDark : DisposalDeviceArt.RockerLight;
        var right = on ? DisposalDeviceArt.RockerLight : DisposalDeviceArt.RockerDark;
        brush.Rect(x, y, PowerWidth, KeyHeight, DisposalDeviceArt.KeyInk);
        brush.Rect(x + 1, y + 1, 6, 9, left);
        brush.Rect(x + 7, y + 1, 6, 9, right);
        brush.Rect(x + (on ? 7 : 1), y + 1, 6, 1, DisposalDeviceArt.KeyFace);
        brush.Rect(x + 2, y + 3, 4, 5, DisposalDeviceArt.KeyInk);
        brush.Rect(x + 3, y + 4, 2, 3, left);
        brush.Rect(x + 9, y + 3, 2, 5, model.Powered ? DisposalDeviceArt.PowerMark : DisposalDeviceArt.KeyInk);

        x = Layout.EjectX;
        var depth = brush.Key(x, y, EjectWidth, KeyHeight, DisposalDeviceArt.KeyFace, DisposalDeviceArt.KeyHigh, DisposalDeviceArt.KeyLow, _eject.Held);
        brush.Arrow(x + 6, y + 2 + depth, true, DisposalDeviceArt.KeyInk, 3);
        brush.Rect(x + 5, y + 5 + depth, 3, 1, DisposalDeviceArt.KeyInk);
        brush.Rect(x + 3, y + 7 + depth, 7, 1, DisposalDeviceArt.KeyInk);

        x = Layout.CloseX;
        depth = brush.Key(x, y, Layout.CloseWidth, KeyHeight, DisposalDeviceArt.KeyDark, DisposalDeviceArt.KeyDarkHigh, DisposalDeviceArt.KeyDarkLow, _close.Held);
        var center = x + (Layout.CloseWidth >> 1);
        for (var i = 0; i < 5; i++)
        {
            brush.Rect(center - 2 + i, y + 2 + i + depth, 1, 1, DisposalDeviceArt.Cross);
            brush.Rect(center + 2 - i, y + 2 + i + depth, 1, 1, DisposalDeviceArt.Cross);
        }
    }

    /// <summary>
    /// The paper slip in its holder on the plinth.
    /// </summary>
    protected void DrawSlip(DisposalDeviceBrush brush, string text, bool locked)
    {
        var x = Layout.SlipX;
        var y = Layout.KeyRow;
        var width = Layout.SlipWidth;
        brush.Rect(x, y, width, KeyHeight, DisposalDeviceArt.KeyInk);
        brush.Rect(x + 1, y + 1, width - 2, 9, DisposalDeviceArt.Paper);
        brush.Rect(x + 1, y + 8, width - 2, 2, DisposalDeviceArt.PaperLow);
        if (!locked)
        {
            brush.Text(Fit(text, width - 4), x + 2, y + 3, DisposalDeviceArt.Ink);
            return;
        }

        brush.Text(Fit(text, width - 13), x + 2, y + 3, DisposalDeviceArt.PaperFaint);
        brush.Rect(x + width - 8, y + 5, 5, 4, DisposalDeviceArt.Ink);
        brush.Rect(x + width - 7, y + 3, 3, 2, DisposalDeviceArt.Ink);
        brush.Rect(x + width - 6, y + 4, 1, 1, DisposalDeviceArt.Paper);
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();

        Grip.Drop();
        _probe.Release();
        _drops.Clear();
        _climbs.Clear();
    }

    private sealed class Drop(EntityUid uid, float time, int direction, int scale)
    {
        public readonly EntityUid Uid = uid;
        public readonly int Direction = direction;
        public readonly int Scale = scale;
        public float Time = time;
        public float Waited;
    }

    private sealed class Climb(EntityUid uid, float time, int side, int id)
    {
        public readonly EntityUid Uid = uid;
        public readonly int Side = side;
        public readonly int Id = id;
        public float Time = time;
        public float Waited;
    }
}
