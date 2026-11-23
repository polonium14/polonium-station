using Content.Client._Polonium.UserInterface.PixelUi;
using Content.Shared.Disposal.Components;
using Robust.Client.UserInterface;

namespace Content.Client._Polonium.Disposal.UI;

/// <summary>
/// The blue mailing unit: an envelope plate, pressure grooves, a board of addresses and a lever on its side.
/// </summary>
public sealed class MailingUnitDeviceWindow : DisposalDeviceWindow
{
    private const int BoardX = 9;
    private const int BoardY = 28;
    private const int BoardWidth = 94;
    private const int BoardHeight = 44;
    private const int BoardRows = 4;
    private const int RowPitch = 10;
    private const int RailHeight = 16;
    private const int LampSize = 3;
    private const int GrooveRows = 3;
    private const int GrooveMarks = 12;
    private const int PivotX = 112;
    private const int PivotY = 79;
    private const int LeverLength = 36;
    private const int BeltY = 79;
    private const float FlapClose = 0.45f;
    private const float DimTime = 0.3f;
    private const float DimDepth = 0.68f;
    private const float LampHalo = 0.3f;
    private const float LampHaloDim = 0.18f;

    private static readonly UIBox2i BodyArea = UIBox2i.FromDimensions(4, 4, 104, 126);
    private static readonly UIBox2i LeverReach = UIBox2i.FromDimensions(106, 36, 26, 86);

    private static readonly Color AddressLamp = Color.FromHex("#ffd24a");
    private static readonly Color AddressLampDim = Color.FromHex("#b8902e");
    private static readonly Color AddressLampOff = Color.FromHex("#3a3320");
    private static readonly Color Envelope = Color.FromHex("#c6c6c6");
    private static readonly Color EnvelopeLow = Color.FromHex("#a9a9a9");
    private static readonly Color EnvelopeLine = Color.FromHex("#737373");
    private static readonly Color EnvelopeFullLine = Color.FromHex("#8e8e8e");
    private static readonly Color GrooveOff = Color.FromHex("#0b3767");
    private static readonly Color Rail = Color.FromHex("#1a2030");
    private static readonly Color RailThumb = Color.FromHex("#7fb0e0");
    private static readonly Color NoTarget = Color.FromHex("#e0a03a");
    private static readonly Color DimShade = Color.FromHex("#04060e");

    private static readonly DisposalDeviceLayout UnitLayout = new(
        "mailing.png",
        new Vector2i(132, 138),
        0,
        UIBox2i.FromDimensions(20, 91, 68, 14),
        LeverReach,
        118,
        9,
        25,
        40,
        46,
        88,
        15);

    private readonly DisposalDeviceKey[] _rows = new DisposalDeviceKey[BoardRows];
    private readonly DisposalDeviceKey _up;
    private readonly DisposalDeviceKey _down;
    private readonly Control _envelope;
    private readonly Control _belt;
    private readonly Control _deadBoard;
    private readonly string _to;
    private readonly string _noTarget;
    private readonly string _pickFirst;
    private readonly string _flush;
    private readonly string _flushPickFirst;
    private readonly string _inside;
    private readonly string _empty;
    private readonly string _ownNone;
    private float _dim;
    private int _scroll;

    /// <summary>
    /// Raised by a click on an address.
    /// </summary>
    public event Action<string>? OnTarget;

    protected override float SlamTime => FlapClose;

    public MailingUnitDeviceWindow() : base(UnitLayout)
    {
        _to = Loc.GetString("polonium-mailing-to");
        _noTarget = Loc.GetString("polonium-mailing-no-target");
        _pickFirst = Loc.GetString("polonium-mailing-pick-first");
        _flush = Loc.GetString("polonium-disposal-flush");
        _flushPickFirst = Loc.GetString("polonium-mailing-flush-pick-first");
        _inside = Loc.GetString("polonium-disposal-inside");
        _empty = Loc.GetString("polonium-disposal-empty");
        _ownNone = Loc.GetString("polonium-mailing-own-none");

        Slip.Disabled = true;

        AddLayer(UnitLayout.Mouth, DrawFlap);

        _envelope = AddInfo(UIBox2i.FromDimensions(9, 8, 28, 17));
        AddStatusArea(UIBox2i.FromDimensions(41, 9, 62, 14));
        _belt = AddInfo(UIBox2i.FromDimensions(5, 76, 100, 10));

        for (var i = 0; i < BoardRows; i++)
        {
            var row = i;
            var key = AddKey(UIBox2i.FromDimensions(BoardX + 2, BoardY + 3 + i * RowPitch, 79, RowPitch), null);
            key.OnPressed += _ => PressRow(row);
            key.OnWheel += Scroll;
            _rows[i] = key;
        }

        _up = AddKey(UIBox2i.FromDimensions(BoardX + 82, BoardY + 1, 11, 12), Loc.GetString("polonium-mailing-up"));
        _up.OnPressed += _ => Scroll(-1);
        _up.OnWheel += Scroll;

        _down = AddKey(UIBox2i.FromDimensions(BoardX + 82, BoardY + 31, 11, 12), Loc.GetString("polonium-mailing-down"));
        _down.OnPressed += _ => Scroll(1);
        _down.OnWheel += Scroll;

        _deadBoard = AddInfo(UIBox2i.FromDimensions(BoardX, BoardY, BoardWidth, BoardHeight));
        _deadBoard.ToolTip = NoPowerText;
        _deadBoard.Visible = false;
    }

    private Control AddInfo(UIBox2i art)
    {
        var control = new Control { MouseFilter = MouseFilterMode.Pass };
        AddPart(control, art);
        return control;
    }

    protected override void OnModelSet(DisposalDeviceModel model)
    {
        _dim = model.Powered ? 0f : 1f;
        var index = model.MailTarget == null ? -1 : IndexOf(model.MailTargets, model.MailTarget);
        _scroll = Math.Clamp(index - 1, 0, MaxScroll(model));
    }

    private static int IndexOf(IReadOnlyList<string> list, string value)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] == value)
                return i;
        }

        return -1;
    }

    private static int MaxScroll(DisposalDeviceModel model)
    {
        return Math.Max(0, model.MailTargets.Count - BoardRows);
    }

    private void Scroll(int by)
    {
        if (Model is { Powered: true } model)
            _scroll = Math.Clamp(_scroll + by, 0, MaxScroll(model));
    }

    private void PressRow(int row)
    {
        if (Model is not { Powered: true } model)
            return;

        var index = _scroll + row;
        if (index < model.MailTargets.Count)
            OnTarget?.Invoke(model.MailTargets[index]);
    }

    protected override float LeverAt(float from, float delta)
    {
        var cosine = Math.Clamp(MathF.Cos(MathF.PI * from) - delta / LeverLength, -1f, 1f);
        return MathF.Acos(cosine) / MathF.PI;
    }

    protected override bool OnBody(Vector2i point)
    {
        return BodyArea.Contains(point) || LeverReach.Contains(point);
    }

    protected override void OnLeverLetGo(DisposalDeviceModel model)
    {
        if (model.Powered && string.IsNullOrEmpty(model.MailTarget))
            Popup(_pickFirst);
    }

    protected override void UpdateUnit(DisposalDeviceModel model, float frameTime)
    {
        var step = ReducedMotion ? 1f : frameTime / DimTime;
        _dim += Math.Clamp((model.Powered ? 0f : 1f) - _dim, -step, step);
        _scroll = Math.Clamp(_scroll, 0, MaxScroll(model));

        var live = model.Powered;
        for (var i = 0; i < BoardRows; i++)
        {
            var index = _scroll + i;
            var exists = index < model.MailTargets.Count;
            _rows[i].Visible = exists;
            _rows[i].Disabled = !live;
            if (exists)
                _rows[i].SetLiveToolTip(Text("polonium-mailing-target", "tag", model.MailTargets[index]));
        }

        _up.Disabled = !live;
        _down.Disabled = !live;
        _deadBoard.Visible = !live;

        var hasTarget = !string.IsNullOrEmpty(model.MailTarget);
        Grip.SetLiveToolTip(hasTarget ? _flush : _flushPickFirst);
        _belt.SetLiveToolTip(hasTarget ? Text("polonium-mailing-target", "tag", model.MailTarget!) : _pickFirst);
        _envelope.SetLiveToolTip(model.Contents.Count > 0 ? _inside : _empty);
        Slip.SetLiveToolTip(string.IsNullOrEmpty(model.MailTag) ? _ownNone : Text("polonium-mailing-own", "tag", model.MailTag));
    }

    protected override void DrawUnit(DisposalDeviceBrush brush, DisposalDeviceModel model)
    {
        DrawEnvelope(brush, model);
        DrawGrooves(brush, model);
        DrawBoard(brush, model);
        DrawBelt(brush, model);
        DrawSlip(brush, model.MailTag ?? string.Empty, false);
        DrawSideLever(brush);
    }

    private static void DrawEnvelope(DisposalDeviceBrush brush, DisposalDeviceModel model)
    {
        var full = model.Contents.Count > 0 && model.Powered;
        var top = full ? Color.White : Envelope;
        var body = full ? Envelope : EnvelopeLow;
        var line = full ? EnvelopeFullLine : EnvelopeLine;
        brush.Rect(10, 9, 26, 15, body);
        brush.Rect(10, 9, 26, 4, top);
        for (var i = 0; i < 7; i++)
        {
            brush.Rect(10 + i * 2, 10 + i, 2, 1, line);
            brush.Rect(34 - i * 2, 10 + i, 2, 1, line);
        }
    }

    private static void DrawGrooves(DisposalDeviceBrush brush, DisposalDeviceModel model)
    {
        var lit = model.Powered ? (int) (model.Pressure * GrooveRows * GrooveMarks + 0.001f) : 0;
        for (var row = 0; row < GrooveRows; row++)
        {
            for (var mark = 0; mark < GrooveMarks; mark++)
            {
                var on = (GrooveRows - 1 - row) * GrooveMarks + mark < lit;
                brush.Rect(43 + mark * 5, 10 + row * 5, 4, 2, on ? Envelope : GrooveOff);
                if (on)
                    brush.Rect(43 + mark * 5, 10 + row * 5, 4, 1, Color.White);
            }
        }
    }

    private void DrawBoard(DisposalDeviceBrush brush, DisposalDeviceModel model)
    {
        var live = model.Powered;
        var targets = model.MailTargets;
        brush.Rect(BoardX, BoardY, BoardWidth, BoardHeight, DisposalDeviceArt.MailEdge);
        brush.Rect(BoardX + 1, BoardY + 1, BoardWidth - 2, BoardHeight - 2, DisposalDeviceArt.Board);

        for (var row = 0; row < BoardRows; row++)
        {
            var index = _scroll + row;
            if (index >= targets.Count)
                continue;

            var top = BoardY + 3 + row * RowPitch;
            var chosen = targets[index] == model.MailTarget;
            var lit = live && chosen;
            var faint = lit && model.Engaged && BlinkDim;
            if (lit)
                brush.Glow(BoardX + 4, top + 3, LampSize, faint ? AddressLampDim : AddressLamp, faint ? LampHaloDim : LampHalo);

            brush.Rect(BoardX + 4, top + 3, LampSize, LampSize, lit ? faint ? AddressLampDim : AddressLamp : AddressLampOff);
            var face = chosen ? DisposalDeviceArt.Paper : live && _rows[row].Hovered ? DisposalDeviceArt.FlapHigh : DisposalDeviceArt.Flap;
            brush.Rect(BoardX + 10, top, 70, 9, face);
            brush.Text(Fit(targets[index], 64), BoardX + 13, top + 2, chosen ? DisposalDeviceArt.Ink : DisposalDeviceArt.FlapText);
        }

        var depth = brush.Key(BoardX + 83, BoardY + 2, 9, 10, DisposalDeviceArt.KeyFace, DisposalDeviceArt.KeyHigh, DisposalDeviceArt.KeyLow, live && _up.Held);
        brush.Arrow(BoardX + 87, BoardY + 5 + depth, true, DisposalDeviceArt.KeyInk, 3);
        depth = brush.Key(BoardX + 83, BoardY + 32, 9, 10, DisposalDeviceArt.KeyFace, DisposalDeviceArt.KeyHigh, DisposalDeviceArt.KeyLow, live && _down.Held);
        brush.Arrow(BoardX + 87, BoardY + 35 + depth, false, DisposalDeviceArt.KeyInk, 3);

        var max = Math.Max(1, targets.Count - BoardRows);
        var thumb = Math.Max(4, (int) MathF.Round(RailHeight * BoardRows / (float) Math.Max(BoardRows, targets.Count)));
        brush.Rect(BoardX + 86, BoardY + 14, 3, RailHeight, Rail);
        brush.Rect(BoardX + 86, BoardY + 14 + (int) MathF.Round((RailHeight - thumb) * _scroll / (float) max), 3, thumb, RailThumb);

        Dimmed(brush, BoardX + 1, BoardY + 1, BoardWidth - 2, BoardHeight - 2);
    }

    private void DrawBelt(DisposalDeviceBrush brush, DisposalDeviceModel model)
    {
        var x = 9 + brush.Text(_to, 9, BeltY, RailThumb) + 2;
        if (string.IsNullOrEmpty(model.MailTarget))
            brush.Text(_noTarget, x, BeltY, NoTarget);
        else
            brush.Text(Fit(model.MailTarget, 103 - x), x, BeltY, Color.White);

        Dimmed(brush, 5, 77, 102, 9);
    }

    /// <summary>
    /// The board goes dark in four even steps, so it never flickers.
    /// </summary>
    private void Dimmed(DisposalDeviceBrush brush, int x, int y, int width, int height)
    {
        if (_dim > 0f)
            brush.Rect(x, y, width, height, DimShade.WithAlpha(DisposalDeviceArt.Steps(_dim, 4) * DimDepth));
    }

    /// <summary>
    /// The lever on the side swings towards the viewer on its way down.
    /// </summary>
    private void DrawSideLever(DisposalDeviceBrush brush)
    {
        var angle = MathF.PI * DisposalDeviceArt.Ease(LeverPosition);
        var knobX = PivotX + (int) MathF.Round(8f * MathF.Sin(angle));
        var knobY = PivotY - (int) MathF.Round(LeverLength * MathF.Cos(angle));
        var steps = Math.Max(Math.Abs(knobX - PivotX), Math.Abs(knobY - PivotY));
        for (var i = 0; i <= steps; i++)
        {
            var part = steps == 0 ? 0f : i / (float) steps;
            var x = (int) MathF.Round(PivotX + (knobX - PivotX) * part) - 2;
            var y = (int) MathF.Round(PivotY + (knobY - PivotY) * part) - 2;
            brush.Rect(x, y, 4, 4, DisposalDeviceArt.Lever);
            brush.Rect(x, y, 1, 4, DisposalDeviceArt.LeverHigh);
        }

        var size = 10 + (int) MathF.Round(2f * MathF.Sin(angle));
        var left = knobX - size / 2;
        var top = knobY - size / 2;
        brush.Rect(left, top, size, size, DisposalDeviceArt.LeverLow);
        brush.Rect(left, top, size, size - 2, DisposalDeviceArt.Lever);
        brush.Rect(left, top, size, 3, DisposalDeviceArt.LeverHigh);
    }

    /// <summary>
    /// The flap of the tray: it drops for the flush and stays shut until pressure is back. A thing pushed through swings it.
    /// </summary>
    private void DrawFlap(DisposalDeviceBrush brush)
    {
        if (Model is not { } model)
            return;

        var charging = model.State != DisposalsPressureState.Ready;
        var age = model.SinceFlush;
        var amount = !ReducedMotion && age < FlapClose ? DisposalDeviceArt.Steps(age / FlapClose, 3) : charging ? 1f : 0f;
        if (amount > 0f && Kick > 0f)
            amount = 1f / 3f;

        var mouth = Layout.Mouth;
        var height = (int) MathF.Round(amount * mouth.Height);
        if (height <= 0)
            return;

        brush.Rect(mouth.Left, mouth.Top, mouth.Width, height, DisposalDeviceArt.MailFace);
        brush.Rect(mouth.Left, mouth.Top, mouth.Width, 1, DisposalDeviceArt.MailHigh);
        brush.Rect(mouth.Left, mouth.Top + height - 2, mouth.Width, 1, DisposalDeviceArt.MailMid);
        brush.Rect(mouth.Left, mouth.Top + height - 1, mouth.Width, 1, DisposalDeviceArt.MailEdge);
    }
}
