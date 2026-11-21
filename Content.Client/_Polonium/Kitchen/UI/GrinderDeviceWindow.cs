using System.Numerics;
using Content.Client._Polonium.UserInterface.PixelUi;
using Content.Client.Resources;
using Content.Shared.CCVar;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Kitchen;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Kitchen.UI;

/// <summary>
/// The reagent grinder drawn as a kitchen blender with a note of its contents pinned next to it.
/// </summary>
public sealed partial class GrinderDeviceWindow : BaseWindow
{
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IResourceCache _cache = default!;

    private const int NoteWidth = 108;
    private const int ChamberTop = 11;
    private const int ChamberRows = 7;
    private const int VesselTop = 90;
    private const int VesselRows = 10;
    private const int CaptionTop = 201;
    private const int AutoCaptionTop = 266;
    private const int LeftAxis = 42;
    private const int GrindAxis = 81;
    private const int JuiceAxis = 135;
    private const int RightAxis = 174;
    private const int KeyRow = 187;
    private const int BaseTop = 171;
    private const int BaseBottom = 291;
    private const int BaseTopLeft = 26;
    private const int BaseBottomLeft = 4;
    private const int BaseEdges = 216;
    private const string OffMark = "0";

    private static readonly Vector2i BodyAt = new(5, 157);
    private static readonly Vector2i LampAt = new(104, 183);
    private static readonly UIBox2i Canvas = UIBox2i.FromDimensions(0, 0, GrinderArt.CanvasWidth, GrinderArt.CanvasHeight);
    private static readonly UIBox2i NoteArea = UIBox2i.FromDimensions(224, 28, 112, 200);
    private static readonly UIBox2i NotePaper = UIBox2i.FromDimensions(224, 32, NoteWidth, 196);
    private static readonly UIBox2i LampArea = UIBox2i.FromDimensions(103, 182, 11, 11);
    private static readonly UIBox2i Knob = Around(LeftAxis, 250, 13);
    private static readonly UIBox2i[] Marks = [Around(LeftAxis, 229, 6), Around(LeftAxis - 18, 237, 6), Around(LeftAxis + 18, 237, 6)];
    private static readonly UIBox2[] KnobFaces = [GrinderArt.KnobOff, GrinderArt.KnobGrind, GrinderArt.KnobJuice];
    private static readonly UIBox2?[] MarkIcons = [null, GrinderArt.GrindMark, GrinderArt.JuiceMark];
    private static readonly LocId[] MarkTips = ["polonium-grinder-auto-off", "polonium-grinder-auto-grind", "polonium-grinder-auto-juice"];

    private static readonly Color DarkPrint = Color.FromHex("#262626");
    private static readonly Color LightPrint = Color.FromHex("#f2f2f2");
    private static readonly Color DimPrint = Color.FromHex("#c4c4c4");

    private static readonly LocId ReagentRow = "polonium-grinder-note-reagent";
    private static readonly LocId VolumeTally = "polonium-grinder-note-volume";
    private static readonly ProtoId<SoundCollectionPrototype> KnobSounds = "PixelKeyDown";

    private readonly PixelFont _font;
    private readonly Texture _body;
    private readonly Texture _controls;
    private readonly GrinderSpriteProbe _probe;
    private readonly GrinderJug _jug;
    private readonly GrinderNiche _niche;
    private readonly GrinderKey _grind;
    private readonly GrinderKey _juice;
    private readonly GrinderKey _ejectChamber;
    private readonly GrinderKey _ejectVessel;
    private readonly GrinderKey _knob;
    private readonly GrinderMark[] _marks = new GrinderMark[Marks.Length];
    private readonly Control _lampArea;
    private readonly PixelNote _note;
    private readonly PixelNoteSection _chamberList;
    private readonly PixelNoteSection _vesselList;
    private readonly List<PixelNoteRow> _chamberRows = new();
    private readonly List<PixelNoteRow> _vesselRows = new();
    private readonly List<(Control Control, UIBox2i Art)> _layout = new();
    private readonly PixelKeySoundUIController _keySounds;
    private readonly SoundSpecifier _knobSound = new SoundCollectionSpecifier(KnobSounds);
    private readonly string _grindCaption;
    private readonly string _juiceCaption;
    private readonly string _autoCaption;
    private readonly string _grindTip;
    private readonly string _juiceTip;
    private readonly string _noPower;
    private readonly string _noVessel;
    private readonly string _vesselReady;
    private readonly string _chamberEmpty;
    private readonly string _cannotGrind;
    private readonly string _cannotJuice;
    private readonly string _noVesselHeading;
    private readonly string _emptyText;
    private readonly int _grindCaptionWidth;
    private readonly int _juiceCaptionWidth;
    private readonly int _autoCaptionWidth;

    private GrinderDeviceModel? _model;
    private UIBox2 _lamp = GrinderArt.LampOff;
    private int _noteStamp;
    private bool _noteWritten;

    public event Action<GrinderProgram>? OnStart;
    public event Action<GrinderAutoMode>? OnAutoSelected;
    public event Action? OnEjectAll;
    public event Action<EntityUid>? OnEject;

    /// <summary>
    /// Raised by a click on the vessel or on its empty place. The game takes the vessel out or puts in the one from the hand.
    /// </summary>
    public event Action? OnVesselPressed;

    public GrinderDeviceWindow()
    {
        IoCManager.InjectDependencies(this);

        _font = PixelFont.Tiny5(_cache);
        _body = _cache.GetTexture(GrinderArt.TexturePath + "body.png");
        _controls = _cache.GetTexture(GrinderArt.TexturePath + "controls.png");
        _keySounds = UserInterfaceManager.GetUIController<PixelKeySoundUIController>();
        _probe = new GrinderSpriteProbe(_entMan, _clyde);

        _grindCaption = Loc.GetString("polonium-grinder-caption-grind");
        _juiceCaption = Loc.GetString("polonium-grinder-caption-juice");
        _autoCaption = Loc.GetString("polonium-grinder-caption-auto");
        _grindCaptionWidth = _font.Measure(_grindCaption) - 1;
        _juiceCaptionWidth = _font.Measure(_juiceCaption) - 1;
        _autoCaptionWidth = _font.Measure(_autoCaption) - 1;
        _grindTip = Loc.GetString("grinder-menu-grind-button");
        _juiceTip = Loc.GetString("grinder-menu-juice-button");
        _noPower = Loc.GetString("polonium-grinder-no-power");
        _noVessel = Loc.GetString("polonium-grinder-no-vessel");
        _vesselReady = Loc.GetString("polonium-grinder-vessel-ready");
        _chamberEmpty = Loc.GetString("polonium-grinder-chamber-empty");
        _cannotGrind = Loc.GetString("polonium-grinder-cannot-grind");
        _cannotJuice = Loc.GetString("polonium-grinder-cannot-juice");
        _noVesselHeading = Loc.GetString("polonium-grinder-note-no-vessel");
        _emptyText = Loc.GetString("polonium-grinder-note-empty");

        MouseFilter = MouseFilterMode.Stop;
        Resizable = false;

        _jug = new GrinderJug(_probe);
        _jug.OnItemPressed += uid => OnEject?.Invoke(uid);
        _jug.OnHoverChanged += SetHovered;

        _niche = new GrinderNiche(_probe, _jug.Painter);
        _niche.OnPressed += () => OnVesselPressed?.Invoke();
        Add(_niche, GrinderNiche.Area);
        Add(_jug, GrinderJug.Area);

        _grind = AddKey(Around(GrindAxis, KeyRow, 12), GrinderArt.LargeKey, GrinderArt.LargeKeyDown, GrinderArt.GrindIcon);
        _grind.OnPressed += _ => OnStart?.Invoke(GrinderProgram.Grind);

        _juice = AddKey(Around(JuiceAxis, KeyRow, 12), GrinderArt.LargeKey, GrinderArt.LargeKeyDown, GrinderArt.JuiceIcon);
        _juice.OnPressed += _ => OnStart?.Invoke(GrinderProgram.Juice);

        _ejectChamber = AddKey(Around(LeftAxis, KeyRow, 10), GrinderArt.MediumKey, GrinderArt.MediumKeyDown, GrinderArt.ChamberIcon);
        _ejectChamber.ToolTip = Loc.GetString("grinder-menu-chamber-content-box-button");
        _ejectChamber.OnPressed += _ => OnEjectAll?.Invoke();

        var close = AddKey(Around(RightAxis, KeyRow, 8), GrinderArt.SmallKey, GrinderArt.SmallKeyDown, GrinderArt.CloseIcon);
        close.ToolTip = Loc.GetString("polonium-grinder-close");
        close.OnPressed += _ => Close();

        _ejectVessel = AddKey(Around(RightAxis, 234, 10), GrinderArt.MediumKey, GrinderArt.MediumKeyDown, GrinderArt.EjectIcon);
        _ejectVessel.ToolTip = Loc.GetString("polonium-grinder-eject-vessel");
        _ejectVessel.OnPressed += _ => OnVesselPressed?.Invoke();

        _knob = new GrinderKey
        {
            Atlas = _controls,
            Face = GrinderArt.KnobOff,
            FaceDown = GrinderArt.KnobOff,
            MuteSounds = true,
            ToolTip = Loc.GetString("grinder-menu-auto-label"),
        };
        _knob.OnPressed += _ => TurnKnob((GrinderAutoMode) (((int) (_model?.AutoMode ?? GrinderAutoMode.Off) + 1) % Marks.Length));
        Add(_knob, Knob);

        for (var i = 0; i < Marks.Length; i++)
        {
            var mode = (GrinderAutoMode) i;
            var mark = new GrinderMark
            {
                Atlas = _controls,
                Icon = MarkIcons[i],
                Font = _font,
                Text = MarkIcons[i] == null ? OffMark : string.Empty,
                ToolTip = Loc.GetString(MarkTips[i]),
            };
            mark.OnPressed += _ => TurnKnob(mode);
            Add(mark, Marks[i]);
            _marks[i] = mark;
        }

        _lampArea = new Control { MouseFilter = MouseFilterMode.Pass };
        Add(_lampArea, LampArea);

        _note = new PixelNote(_cache.GetTexture(GrinderArt.TexturePath + "note.png"), PixelFont.Plain(_cache), NoteWidth) { MouseFilter = MouseFilterMode.Pass };
        _chamberList = _note.AddSection(ChamberTop, ChamberRows);
        _chamberList.Heading = Loc.GetString("grinder-menu-chamber-content-box-label").ToUpperInvariant();
        _chamberList.Empty = _emptyText;
        _vesselList = _note.AddSection(VesselTop, VesselRows);
        _vesselList.More = Loc.GetString("polonium-grinder-note-more");
        _vesselList.End = Loc.GetString("polonium-grinder-note-end");
        _note.OnItemPressed += uid => OnEject?.Invoke(uid);
        _note.OnHoverChanged += SetHovered;
        Add(_note, NoteArea);
    }

    /// <summary>
    /// Tells the window which grinder it shows.
    /// </summary>
    public void SetEntity(EntityUid grinder)
    {
        _model = new GrinderDeviceModel(grinder, _entMan, _proto, _timing, _player);
    }

    private static UIBox2i Around(int centerX, int centerY, int radius)
    {
        return UIBox2i.FromDimensions(centerX - radius, centerY - radius, radius * 2 + 1, radius * 2 + 1);
    }

    private void Add(Control control, UIBox2i art)
    {
        AddChild(control);
        _layout.Add((control, art));
    }

    private GrinderKey AddKey(UIBox2i art, UIBox2 face, UIBox2 faceDown, UIBox2 icon)
    {
        var key = new GrinderKey
        {
            Atlas = _controls,
            Face = face,
            FaceDown = faceDown,
            Icon = icon,
        };
        _keySounds.Attach(key);
        Add(key, art);
        return key;
    }

    private void TurnKnob(GrinderAutoMode mode)
    {
        _keySounds.PlaySignal(_knobSound);
        OnAutoSelected?.Invoke(mode);
    }

    private void SetHovered(EntityUid? uid)
    {
        _jug.Highlighted = uid;
        _note.Highlighted = uid;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_model is not { } model)
            return;

        var reduced = _cfg.GetCVar(CCVars.ReducedMotion);
        model.Refresh();
        _jug.Advance(model, args.DeltaSeconds, reduced);
        _niche.Advance(model, _jug.Stream, args.DeltaSeconds, reduced);
        _note.ReducedMotion = reduced;
        _note.Interactive = !model.Busy;
        UpdateControls(model);
        UpdateNote(model);
    }

    private void UpdateControls(GrinderDeviceModel model)
    {
        var busy = model.Busy;
        var vessel = model.Vessel != null;

        var grindFault = Fault(model, model.CanGrind, _cannotGrind);
        _grind.Disabled = busy || grindFault != null;
        _grind.Latched = model.Program == GrinderProgram.Grind;
        _grind.SetLiveToolTip(grindFault ?? _grindTip);

        var juiceFault = Fault(model, model.CanJuice, _cannotJuice);
        _juice.Disabled = busy || juiceFault != null;
        _juice.Latched = model.Program == GrinderProgram.Juice;
        _juice.SetLiveToolTip(juiceFault ?? _juiceTip);

        _ejectChamber.Disabled = busy || model.Contents.Count == 0;
        _ejectVessel.Disabled = busy || !vessel;

        var auto = Math.Clamp((int) model.AutoMode, 0, Marks.Length - 1);
        _knob.Face = KnobFaces[auto];
        _knob.FaceDown = KnobFaces[auto];
        for (var i = 0; i < _marks.Length; i++)
        {
            _marks[i].Tint = i == auto ? Color.White : DimPrint;
        }

        _lamp = !model.Powered ? GrinderArt.LampOff : vessel ? GrinderArt.LampGreenCell : GrinderArt.LampRed;
        _lampArea.SetLiveToolTip(!model.Powered ? _noPower : vessel ? _vesselReady : _noVessel);
    }

    /// <summary>
    /// Why a program cannot be started right now. Null when it can, and also while the grinder is busy.
    /// </summary>
    private string? Fault(GrinderDeviceModel model, bool fits, string unfit)
    {
        if (model.Busy)
            return null;

        if (!model.Powered)
            return _noPower;

        if (model.Vessel == null)
            return _noVessel;

        if (model.Contents.Count == 0)
            return _chamberEmpty;

        return fits ? null : unfit;
    }

    private void UpdateNote(GrinderDeviceModel model)
    {
        // The note is rewritten once, when the server has said what the run made, and not with every guess before that.
        if (model.Settling && _noteWritten)
            return;

        var stamp = new HashCode();
        stamp.Add(model.Capacity);
        for (var i = 0; i < model.Contents.Count; i++)
        {
            stamp.Add(model.Contents[i]);
            stamp.Add(model.Name(model.Contents[i], out var count));
            stamp.Add(count);
        }

        stamp.Add(model.Vessel);
        if (model.Vessel is { } held)
            stamp.Add(model.Name(held, out _));

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

        _chamberRows.Clear();
        for (var i = 0; i < model.Contents.Count; i++)
        {
            var name = model.Name(model.Contents[i], out var count);
            _chamberRows.Add(new PixelNoteRow(model.Contents[i], GrinderDeviceModel.Row(name, count)));
        }

        _chamberList.Tally = $"{model.Contents.Count}/{model.Capacity}";
        _note.SetRows(_chamberList, _chamberRows);

        _vesselRows.Clear();
        _vesselList.Dimmed = model.Vessel == null;
        _vesselList.Heading = model.Vessel is { } vessel ? model.Name(vessel, out _).ToUpperInvariant() : _noVesselHeading;
        _vesselList.Empty = model.Vessel == null ? string.Empty : _emptyText;
        _vesselList.Tally = string.Empty;
        if (model.VesselSolution is { } solution)
        {
            _vesselList.Tally = Loc.GetString(VolumeTally, ("volume", solution.Volume.Double()), ("capacity", solution.MaxVolume.Double()));
            foreach (var (reagent, quantity) in solution.Contents)
            {
                if (quantity <= 0 || !_proto.TryIndex<ReagentPrototype>(reagent.Prototype, out var prototype))
                    continue;

                var text = Loc.GetString(ReagentRow, ("amount", quantity.Double()), ("name", prototype.LocalizedName));
                _vesselRows.Add(new PixelNoteRow(EntityUid.Invalid, text, prototype.SubstanceColor));
            }
        }

        _note.SetRows(_vesselList, _vesselRows);
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
    {
        return DragMode.Move;
    }

    protected override bool HasPoint(Vector2 point)
    {
        var art = (Vector2i) (point * UIScale / PixelCanvas.GetScale(UIScale));
        if (NotePaper.Contains(art))
            return true;

        if (art.Y < BodyAt.Y || art.Y >= BodyAt.Y + _body.Height)
            return false;

        // The base grows wider towards the table, by the same amount on both sides.
        var spread = Math.Clamp((art.Y - BaseTop) / (float) (BaseBottom - BaseTop), 0f, 1f);
        var left = BaseTopLeft + (BaseBottomLeft - BaseTopLeft) * spread;
        return art.X >= left && art.X < BaseEdges - left;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(availableSize);
        }

        return PixelLayout.Box(UIScale, Canvas).Size;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        foreach (var (control, art) in _layout)
        {
            control.Arrange(PixelLayout.Box(UIScale, art));
        }

        return finalSize;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        _probe.Render(handle);

        var canvas = new PixelCanvas(handle, UIScale);
        canvas.Texture(_body, BodyAt.X, BodyAt.Y);
        canvas.Text(_font, GrindAxis - _grindCaptionWidth / 2, CaptionTop, _grindCaption, DarkPrint);
        canvas.Text(_font, JuiceAxis - _juiceCaptionWidth / 2, CaptionTop, _juiceCaption, DarkPrint);
        canvas.Text(_font, LeftAxis - _autoCaptionWidth / 2, AutoCaptionTop, _autoCaption, LightPrint);
        canvas.Region(_controls, _lamp, LampAt.X, LampAt.Y);
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();

        _probe.Release();
    }
}
