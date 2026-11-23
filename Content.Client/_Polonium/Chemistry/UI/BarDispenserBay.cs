using System.Numerics;
using Content.Client._Polonium.UserInterface.PixelUi;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client._Polonium.Chemistry.UI;

/// <summary>
/// A moment in the bay that can be heard.
/// </summary>
public enum BarBayCue : byte
{
    Pour,
    Fizz,
    Cubes,
    Grains,
    Drain,
    VesselIn,
    VesselOut,
}

/// <summary>
/// The recess under the nozzle: the vessel on the drip grille and what falls into it.
/// </summary>
public sealed partial class BarDispenserBay : Control
{
    [Dependency] private IEntityManager _entMan = default!;

    private const int ArtWidth = BarDispenserArt.BayWidth;
    private const int ArtHeight = BarDispenserArt.BayHeight;
    private const int Scale = BarDispenserArt.VesselScale;

    // The nozzle, the stream and the foot of the vessel are centred on the line between two columns.
    private const int Axis = ArtWidth / 2;
    private const int Nozzle = 9;
    private const int Floor = ArtHeight - 10;
    private const int StreamReach = 6;
    private const int EmptyDepth = 30;
    private const int DropIn = 6;
    private const int LeaveRise = 8;
    private const int DrainShift = 8;
    private const int LipRoom = 8;
    private const float DropTime = 0.2f;
    private const float LeaveTime = 0.2f;
    private const float StreamTime = 0.75f;
    private const float SolidTime = 1f;
    private const float StillTime = 0.4f;
    private const float DrainTime = 1f;
    private const float SettlingAlpha = 0.6f;
    private const float GhostAlpha = 0.35f;
    private const float StreamShine = 0.45f;
    private const float DrainShine = 0.35f;
    private const int StillFrame = 3;

    // Frames after which a drink reaches the vessel and the tipped vessel starts to run out.
    private const int LiquidLands = 2;
    private const int CubesLand = 4;
    private const int GrainsLand = 1;
    private const int DrainRuns = 2;

    private const int FullTilt = 68;

    private static readonly int[] Tilt = [20, 45, FullTilt, FullTilt, FullTilt, 45, 20, 0];
    private static readonly int[] CubeColumns = [-3, 0, -5, -1];
    private static readonly UIBox2i Stand = UIBox2i.FromDimensions(1, 10, ArtWidth - 2, ArtHeight - 20);

    private static readonly Color Recess = Color.FromHex("#141414");
    private static readonly Color RecessTop = Color.FromHex("#080808");
    private static readonly Color Steel = Color.FromHex("#b2b2b2");
    private static readonly Color SteelMid = Color.FromHex("#9e9e9e");
    private static readonly Color SteelShade = Color.FromHex("#868686");
    private static readonly Color SteelDark = Color.FromHex("#575757");
    private static readonly Color GrilleEdge = Color.FromHex("#6a6a6a");
    private static readonly Color GrilleBar = Color.FromHex("#444444");
    private static readonly Color GrilleBarTop = Color.FromHex("#5c5c5c");
    private static readonly Color GrilleFoot = Color.FromHex("#2d2d2d");
    private static readonly Color CubeEdge = Color.FromHex("#6d8fa3");
    private static readonly Color Cube = Color.FromHex("#bed8e6");
    private static readonly Color CubeShine = Color.FromHex("#f2fbff");
    private static readonly Color Grain = Color.White;
    private static readonly Color GrainShade = Color.FromHex("#d9d9d9");
    private static readonly Color FizzBubble = Color.White.WithAlpha(0.8f);

    private static readonly LocId TakeTip = "polonium-bar-dispenser-vessel-take";
    private static readonly LocId PutTip = "polonium-bar-dispenser-vessel-put";
    private static readonly LocId NoHandTip = "polonium-bar-dispenser-hand-empty";

    private readonly BarSpriteProbe _probe;
    private readonly BarEntityPainter _painter;
    private readonly List<Stream> _streams = new();
    private readonly string _takeTip;
    private readonly string _noHandTip;
    private EntityUid? _vessel;
    private EntityUid? _leaving;
    private EntityUid? _hand;
    private string _putTip = string.Empty;
    private string _putName = string.Empty;
    private Color _drainColor;
    private float? _drain;
    private float _settled = 1f;
    private float _leave;
    private int _look;
    private bool _reduced;
    private bool _opened;
    private bool _landing;
    private bool _drainHeard;
    private bool _hovered;
    private bool _clickable;

    /// <summary>
    /// Whether the vessel is being emptied over the grille right now.
    /// </summary>
    public bool Draining => _drain != null;

    /// <summary>
    /// Raised by a click. It carries true when the bay is empty and the held vessel should go in.
    /// </summary>
    public event Action<bool>? OnPressed;

    /// <summary>
    /// Raised at the moments that have a sound, in step with what the bay shows.
    /// </summary>
    public event Action<BarBayCue>? OnCue;

    public BarDispenserBay(BarSpriteProbe probe, BarEntityPainter painter)
    {
        IoCManager.InjectDependencies(this);

        _probe = probe;
        _painter = painter;
        _takeTip = Loc.GetString(TakeTip);
        _noHandTip = Loc.GetString(NoHandTip);

        MouseFilter = MouseFilterMode.Pass;
        RectClipContent = true;
    }

    /// <summary>
    /// Lets a drink fall from the nozzle.
    /// </summary>
    public void Pour(Color color, BarStreamKind kind)
    {
        _drain = null;
        _streams.Add(new Stream(color, kind));
    }

    /// <summary>
    /// Tips the vessel over the grille.
    /// </summary>
    /// <param name="color">Colour of what runs out of it.</param>
    public void Drain(Color color)
    {
        _drain = 0f;
        _drainColor = color;
        _drainHeard = false;
    }

    /// <summary>
    /// Brings the bay up to date with the dispenser and moves everything in it on by one frame.
    /// </summary>
    public void Advance(BarDispenserModel model, float delta, bool reduced)
    {
        _reduced = reduced;
        _hand = model.HandVessel;
        if (_hovered && _hand is { } hand)
            _probe.Get(hand);

        if (model.Vessel != _vessel)
        {
            var shown = _opened && !reduced;
            if (_vessel != null && shown)
            {
                _leaving = _vessel;
                _leave = 0f;
            }

            if (_opened)
            {
                if (_vessel != null)
                    OnCue?.Invoke(BarBayCue.VesselOut);

                _landing = model.Vessel != null;
            }

            _vessel = model.Vessel;
            _settled = _vessel != null && shown ? 0f : 1f;
            _drain = null;
            _look = 0;
        }

        _opened = true;
        _settled = MathF.Min(1f, _settled + delta / DropTime);
        if (_landing && _settled >= 1f)
        {
            _landing = false;
            OnCue?.Invoke(BarBayCue.VesselIn);
        }

        if (_leaving != null)
        {
            _leave += delta / LeaveTime;
            if (_leave >= 1f || reduced)
                _leaving = null;
        }

        if (_drain is { } drain)
        {
            if (!_drainHeard && (reduced || BarDispenserArt.Frame(drain) >= DrainRuns))
            {
                _drainHeard = true;
                OnCue?.Invoke(BarBayCue.Drain);
            }

            _drain = drain + delta < (reduced ? StillTime : DrainTime) ? drain + delta : null;
        }

        for (var i = _streams.Count - 1; i >= 0; i--)
        {
            var stream = _streams[i];
            if (!stream.Heard && (reduced || BarDispenserArt.Frame(stream.Age) >= Lands(stream.Kind)))
            {
                stream.Heard = true;
                OnCue?.Invoke(CueOf(stream.Kind));
            }

            stream.Age += delta;
            if (stream.Age >= (reduced ? StillTime : stream.Kind == BarStreamKind.Liquid ? StreamTime : SolidTime))
                _streams.RemoveAt(i);
        }

        WatchLook();
        UpdateHint();
    }

    private static int Lands(BarStreamKind kind)
    {
        return kind switch
        {
            BarStreamKind.Cubes => CubesLand,
            BarStreamKind.Grains => GrainsLand,
            _ => LiquidLands,
        };
    }

    private static BarBayCue CueOf(BarStreamKind kind)
    {
        return kind switch
        {
            BarStreamKind.Fizzy => BarBayCue.Fizz,
            BarStreamKind.Cubes => BarBayCue.Cubes,
            BarStreamKind.Grains => BarBayCue.Grains,
            _ => BarBayCue.Pour,
        };
    }

    /// <summary>
    /// A glass takes the shape of the drink in it, so its picture is measured again whenever its sprite changes.
    /// </summary>
    private void WatchLook()
    {
        if (_vessel is not { } vessel)
            return;

        var look = _probe.Look(vessel);
        if (look == _look)
            return;

        _look = look;
        _probe.Remeasure(vessel);
    }

    private void UpdateHint()
    {
        _clickable = _vessel != null || _hand != null;
        var shape = _clickable ? CursorShape.Hand : CursorShape.Arrow;
        if (DefaultCursorShape != shape)
            DefaultCursorShape = shape;

        if (_vessel != null)
        {
            this.SetLiveToolTip(_takeTip);
            return;
        }

        if (_hand is not { } hand || !_entMan.TryGetComponent(hand, out MetaDataComponent? meta))
        {
            this.SetLiveToolTip(_noHandTip);
            return;
        }

        if (meta.EntityName != _putName)
        {
            _putName = meta.EntityName;
            _putTip = Loc.GetString(PutTip, ("name", _putName));
        }

        this.SetLiveToolTip(_putTip);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var canvas = new PixelCanvas(handle, UIScale);
        DrawRecess(canvas);

        var streamEnd = Floor - EmptyDepth;
        if (_vessel is { } vessel)
        {
            var info = _probe.Get(vessel);
            if (_drain is { } drain && !_reduced)
            {
                DrawDrain(handle, canvas, vessel, info, BarDispenserArt.Frame(drain));
            }
            else
            {
                var above = 1f - _settled;
                DrawStanding(handle, canvas, vessel, info, Floor - BarDispenserArt.Round(above * DropIn), _settled < 1f ? SettlingAlpha : 1f);
                streamEnd = Floor - info.Height * Scale;
                if (_drain != null)
                    canvas.Rect(Axis + 12, Floor - 12, 3, 12, _drainColor);
            }
        }
        else if (_leaving == null && _hovered && _hand is { } hand)
        {
            DrawStanding(handle, canvas, hand, _probe.Get(hand), Floor, GhostAlpha);
        }

        if (_leaving is { } leaving)
            DrawStanding(handle, canvas, leaving, _probe.Get(leaving), Floor - BarDispenserArt.Round(_leave * LeaveRise), 1f - _leave);

        // The nozzle comes after the vessels, so that one taller than the bay stands with its mouth around the nozzle.
        canvas.Rect(Axis - 6, 1, 12, 3, Steel);
        canvas.Rect(Axis - 6, 3, 12, 1, SteelShade);
        canvas.Rect(Axis - 4, 4, 8, 3, SteelMid);
        canvas.Rect(Axis - 4, 6, 8, 1, SteelDark);
        canvas.Rect(Axis - 2, 7, 4, Nozzle - 7, SteelDark);
        foreach (var stream in _streams)
        {
            DrawStream(canvas, stream, Nozzle, Math.Max(Nozzle, streamEnd) + StreamReach);
        }
    }

    private static void DrawRecess(PixelCanvas canvas)
    {
        canvas.Rect(0, 0, ArtWidth, ArtHeight, BarDispenserArt.Ink);
        canvas.Rect(1, 1, ArtWidth - 2, ArtHeight - 2, Recess);
        canvas.Rect(1, 1, ArtWidth - 2, 2, RecessTop);
        canvas.Rect(1, Floor, ArtWidth - 2, 1, GrilleEdge);
        canvas.Rect(1, Floor + 1, ArtWidth - 2, 8, BarDispenserArt.Ink);
        for (var x = 3; x < ArtWidth - 3; x += 4)
        {
            canvas.Rect(x, Floor + 1, 2, 7, GrilleBar);
            canvas.Rect(x, Floor + 1, 2, 1, GrilleBarTop);
        }

        canvas.Rect(1, Floor + 8, ArtWidth - 2, 1, GrilleFoot);
    }

    /// <summary>
    /// Draws a vessel so that the middle of its foot stands under the nozzle on the given row.
    /// </summary>
    private void DrawStanding(DrawingHandleScreen handle, PixelCanvas canvas, EntityUid uid, BarSpriteInfo info, int floor, float alpha)
    {
        if (!info.Measured)
            return;

        var x = BarDispenserArt.Round(Axis - info.Axis * Scale);
        var y = floor - (info.Bottom + 1) * Scale;
        _painter.Draw(handle, canvas, uid, x, y, Scale, Angle.Zero, alpha);
    }

    /// <summary>
    /// Draws the vessel tipped over the right end of its foot, and what runs out of it into the grille.
    /// </summary>
    private void DrawDrain(DrawingHandleScreen handle, PixelCanvas canvas, EntityUid vessel, BarSpriteInfo info, int frame)
    {
        var degrees = Tilt[Math.Min(frame, Tilt.Length - 1)];
        if (!_entMan.TryGetComponent(vessel, out SpriteComponent? sprite) || sprite.NoRotation || sprite.SnapCardinals)
            degrees = 0;

        var turn = Angle.FromDegrees(degrees);
        var sin = (float) Math.Sin(turn.Theta);
        var cos = (float) Math.Cos(turn.Theta);

        // The lip is the upper right corner of the picture. The vessel moves aside far enough for it to stay in the bay.
        var lipX = (info.Right + 1 - info.FootEdge) * Scale;
        var lipY = -info.Height * Scale;
        var full = Angle.FromDegrees(FullTilt);
        var reach = lipX * (float) Math.Cos(full.Theta) - lipY * (float) Math.Sin(full.Theta);
        var pivot = MathF.Min(Axis - DrainShift + (info.FootEdge - info.Axis) * Scale, ArtWidth - LipRoom - reach);
        var offsetX = -info.FootEdge * Scale;
        var offsetY = -(info.Bottom + 1) * Scale;
        var centerX = pivot + offsetX * cos - offsetY * sin;
        var centerY = Floor + offsetX * sin + offsetY * cos;
        _painter.Draw(handle, canvas, vessel, BarDispenserArt.Round(centerX), BarDispenserArt.Round(centerY), Scale, turn);

        if (frame is >= 2 and <= 4)
        {
            var x = BarDispenserArt.Round(pivot + lipX * cos - lipY * sin) - 2;
            var y = BarDispenserArt.Round(Floor + lipX * sin + lipY * cos) + 2;
            canvas.Rect(x, y, 3, Floor - y, _drainColor);
            canvas.Rect(x, y, 1, Floor - y, BarDispenserArt.Lighten(_drainColor, DrainShine));
            canvas.Rect(x - 3, Floor - 1, 9, 1, _drainColor);
        }

        if (frame is < 3 or > 6)
            return;

        for (var i = 0; i < 4; i++)
        {
            canvas.Rect(Axis + 14 + i * 4, Floor + 2 + (frame + i) % 3 * 2, 2, 2, _drainColor);
        }
    }

    private void DrawStream(PixelCanvas canvas, Stream stream, int from, int to)
    {
        var frame = _reduced ? StillFrame : BarDispenserArt.Frame(stream.Age);
        var length = to - from;
        switch (stream.Kind)
        {
            case BarStreamKind.Cubes:
                for (var i = 0; i < CubeColumns.Length; i++)
                {
                    var fall = (frame - i) / 4f;
                    if (fall is < 0f or > 1f)
                        continue;

                    var x = Axis + CubeColumns[i];
                    var y = BarDispenserArt.Round(from + length * fall) - 4;
                    canvas.Rect(x, y, 5, 5, CubeEdge);
                    canvas.Rect(x + 1, y + 1, 3, 3, Cube);
                    canvas.Rect(x + 1, y + 1, 2, 1, CubeShine);
                }

                break;
            case BarStreamKind.Grains:
                for (var i = 0; i < 14; i++)
                {
                    var fall = (frame + i * 0.37f) % 6f / 5f;
                    if (frame > 4 && fall < (frame - 4) / 2f)
                        continue;

                    var y = BarDispenserArt.Round(from + length * MathF.Min(1f, fall + i % 3 * 0.12f));
                    canvas.Rect(Axis - 2 + i * 7 % 4, y, 1, 1, i % 3 == 0 ? GrainShade : Grain);
                }

                break;
            default:
                // The column grows out of the nozzle, stands for a moment and then lets go of the nozzle.
                var head = frame < 4 ? 0f : (frame - 3) / 3f;
                var tail = frame < 2 ? (frame + 1) / 3f : 1f;
                var top = BarDispenserArt.Round(from + length * head);
                var bottom = BarDispenserArt.Round(from + length * tail);
                canvas.Rect(Axis - 2, top, 4, bottom - top, stream.Color);
                canvas.Rect(Axis - 1, top, 1, bottom - top, BarDispenserArt.Lighten(stream.Color, StreamShine));
                if (stream.Kind == BarStreamKind.Fizzy)
                {
                    for (var i = 0; i < 3 && bottom - top > 2; i++)
                    {
                        canvas.Rect(Axis - 2 + (frame + i * 2) % 4, top + (frame * 5 + i * 7) % (bottom - top), 1, 1, FizzBubble);
                    }
                }

                if (tail < 1f || frame >= 5)
                    break;

                var gap = 1 + frame % 2;
                var splash = to - 1 - frame % 2;
                canvas.Rect(Axis - 4 - gap, splash, 2, 1, stream.Color);
                canvas.Rect(Axis + 2 + gap, splash, 2, 1, stream.Color);
                break;
        }
    }

    protected override bool HasPoint(Vector2 point)
    {
        var scale = PixelCanvas.GetScale(UIScale);
        var art = new Vector2i((int) MathF.Floor(point.X * UIScale / scale), (int) MathF.Floor(point.Y * UIScale / scale));
        if (_vessel is not { } vessel)
            return Stand.Contains(art);

        var info = _probe.Get(vessel);
        var left = BarDispenserArt.Round(Axis + (info.Left - info.Axis) * Scale) - 2;
        var height = info.Height * Scale + 2;
        return UIBox2i.FromDimensions(left, Floor - height, info.Width * Scale + 4, height).Contains(art);
    }

    protected override void MouseEntered()
    {
        base.MouseEntered();

        _hovered = true;
    }

    protected override void MouseExited()
    {
        base.MouseExited();

        _hovered = false;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick || !_clickable)
            return;

        OnPressed?.Invoke(_vessel == null);
        args.Handle();
    }

    private sealed class Stream(Color color, BarStreamKind kind)
    {
        public readonly Color Color = color;
        public readonly BarStreamKind Kind = kind;
        public float Age;
        public bool Heard;
    }
}
