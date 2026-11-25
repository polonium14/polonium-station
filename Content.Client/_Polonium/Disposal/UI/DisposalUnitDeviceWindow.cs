using Content.Client._Polonium.UserInterface.PixelUi;
using Content.Shared.Disposal.Components;
using Robust.Client.UserInterface;

namespace Content.Client._Polonium.Disposal.UI;

/// <summary>
/// The yellow disposal unit: a hopper with a hatch, a lever in a slot, three lamps and a thin pressure gauge.
/// </summary>
public sealed class DisposalUnitDeviceWindow : DisposalDeviceWindow
{
    private const int LampSize = 8;
    private const int LampX = 81;
    private const int GaugeX = 7;
    private const int GaugeY = 114;
    private const int GaugeMarks = 29;
    private const int LeverX = 54;
    private const int LeverY = 62;
    private const int LeverWidth = 12;
    private const int LeverHeight = 46;
    private const int HandleHeight = 9;
    private const int HatchBands = 5;
    private const float HatchClose = 0.4f;
    private const float HatchHold = 0.9f;
    private const float HatchOpen = 1.3f;
    private const float HatchRest = 0.2f;
    private const float LampHalo = 0.34f;
    private const float BlueHaloDim = 0.2f;

    private static readonly int[] LampYs = [67, 82, 97];
    private static readonly UIBox2i BodyArea = UIBox2i.FromDimensions(4, 4, 92, 128);

    private static readonly Color RedLit = Color.FromHex("#e85c33");
    private static readonly Color RedShine = Color.FromHex("#feb070");
    private static readonly Color RedOff = Color.FromHex("#5a1c1c");
    private static readonly Color GreenLit = Color.FromHex("#35e765");
    private static readonly Color GreenShine = Color.FromHex("#aeff37");
    private static readonly Color GreenOff = Color.FromHex("#1d4a2a");
    private static readonly Color BlueLit = Color.FromHex("#5fa4e9");
    private static readonly Color BlueShine = Color.FromHex("#abe8fd");
    private static readonly Color BlueDim = Color.FromHex("#4272ae");
    private static readonly Color BlueDimShine = Color.FromHex("#5eabeb");
    private static readonly Color BlueOff = Color.FromHex("#22364f");
    private static readonly Color GaugeOff = Color.FromHex("#3a3c45");

    private static readonly DisposalDeviceLayout UnitLayout = new(
        "disposal.png",
        new Vector2i(100, 162),
        24,
        UIBox2i.FromDimensions(13, 14, 74, 30),
        UIBox2i.FromDimensions(LeverX - 3, LeverY - 1, LeverWidth + 6, LeverHeight + 6),
        118,
        8,
        24,
        39,
        41,
        81,
        13);

    private readonly Control[] _lamps = new Control[LampYs.Length];
    private readonly string _inside;
    private readonly string _empty;
    private readonly string _noAccess;

    /// <summary>
    /// Raised by a click on the routing slip by a player who may change the route.
    /// </summary>
    public event Action? OnRoute;

    protected override bool ShowsCreatures => true;

    protected override float SlamTime => HatchClose;

    public DisposalUnitDeviceWindow() : base(UnitLayout)
    {
        _inside = Loc.GetString("polonium-disposal-inside");
        _empty = Loc.GetString("polonium-disposal-empty");
        _noAccess = Loc.GetString("polonium-disposal-no-access");

        Grip.ToolTip = Loc.GetString("polonium-disposal-flush");

        var mouth = UnitLayout.Mouth;
        AddLayer(UIBox2i.FromDimensions(4, -UnitLayout.Top, 92, mouth.Top + mouth.Height + UnitLayout.Top), DrawClimbs);
        AddLayer(mouth, DrawHatch);

        for (var i = 0; i < _lamps.Length; i++)
        {
            _lamps[i] = new Control { MouseFilter = MouseFilterMode.Pass };
            AddPart(_lamps[i], UIBox2i.FromDimensions(LampX - 2, LampYs[i] - 2, LampSize + 4, LampSize + 4));
        }

        AddStatusArea(UIBox2i.FromDimensions(4, GaugeY - 2, 92, 5));

        Slip.OnPressed += _ => PressSlip();
    }

    private void PressSlip()
    {
        if (Model is not { RouteTag: not null } model)
            return;

        if (model.RouteAllowed)
            OnRoute?.Invoke();
        else
            Popup(_noAccess);
    }

    protected override float LeverAt(float from, float delta)
    {
        return from + delta / (LeverHeight - HandleHeight);
    }

    protected override bool OnBody(Vector2i point)
    {
        return BodyArea.Contains(point);
    }

    protected override void UpdateUnit(DisposalDeviceModel model, float frameTime)
    {
        _lamps[0].ToolTip = model.Powered ? ChargingText : NoPowerText;
        _lamps[1].ToolTip = model.Powered ? ReadyText : NoPowerText;
        _lamps[2].ToolTip = !model.Powered ? NoPowerText : model.Contents.Count > 0 ? _inside : _empty;

        Slip.Disabled = model.RouteTag == null;
        Slip.SetLiveToolTip(model.RouteTag is { } tag
            ? model.RouteAllowed ? Text("polonium-disposal-route", "tag", tag) : _noAccess
            : null);
    }

    protected override void DrawUnit(DisposalDeviceBrush brush, DisposalDeviceModel model)
    {
        DrawLever(brush);
        DrawLamps(brush, model);
        DrawGauge(brush, model);
        DrawSlip(brush, model.RouteTag ?? string.Empty, model.RouteTag != null && !model.RouteAllowed);
    }

    private void DrawLever(DisposalDeviceBrush brush)
    {
        var travel = (int) MathF.Round(DisposalDeviceArt.Ease(LeverPosition) * (LeverHeight - HandleHeight));
        var y = LeverY + travel;
        if (travel > 0)
        {
            brush.Rect(LeverX + 4, LeverY, 4, travel, DisposalDeviceArt.SteelEdge);
            brush.Rect(LeverX + 4, LeverY, 1, travel, DisposalDeviceArt.Steel);
            brush.Rect(LeverX + 7, LeverY, 1, travel, DisposalDeviceArt.Steel);
        }

        brush.Rect(LeverX - 2, y, LeverWidth + 4, HandleHeight, DisposalDeviceArt.RedLow);
        brush.Rect(LeverX - 2, y, LeverWidth + 4, 7, DisposalDeviceArt.RedMid);
        brush.Rect(LeverX - 2, y, LeverWidth + 4, 5, DisposalDeviceArt.Red);
        brush.Rect(LeverX - 1, y + 1, LeverWidth + 2, 1, DisposalDeviceArt.RedHigh);
    }

    private void DrawLamps(DisposalDeviceBrush brush, DisposalDeviceModel model)
    {
        var ready = model.State == DisposalsPressureState.Ready;
        var dim = BlinkDim;
        DrawLamp(brush, 0, model.Powered && !ready, RedLit, RedShine, RedOff, LampHalo);
        DrawLamp(brush, 1, model.Powered && ready, GreenLit, GreenShine, GreenOff, LampHalo);
        DrawLamp(brush, 2, model.Powered && model.Contents.Count > 0 && model.SinceFlush >= 1f,
            dim ? BlueDim : BlueLit, dim ? BlueDimShine : BlueShine, BlueOff, dim ? BlueHaloDim : LampHalo);
    }

    private static void DrawLamp(DisposalDeviceBrush brush, int index, bool lit, Color color, Color shine, Color off, float halo)
    {
        var y = LampYs[index];
        if (lit)
            brush.Glow(LampX, y, LampSize, color, halo);

        brush.Rect(LampX, y, LampSize, LampSize, lit ? color : off);
        if (lit)
            brush.Rect(LampX, y, LampSize / 2, LampSize / 2, shine);
    }

    private static void DrawGauge(DisposalDeviceBrush brush, DisposalDeviceModel model)
    {
        var ready = model.State == DisposalsPressureState.Ready;
        var lit = !model.Powered ? 0 : ready ? GaugeMarks : (int) (model.Pressure * GaugeMarks + 0.001f);
        for (var i = 0; i < GaugeMarks; i++)
        {
            var on = i < lit;
            var x = GaugeX + i * 3;
            brush.Rect(x, GaugeY, 2, 2, !on ? GaugeOff : ready ? GreenLit : RedLit);
            if (on)
                brush.Rect(x, GaugeY, 2, 1, ready ? GreenShine : RedShine);
        }
    }

    /// <summary>
    /// Two plates meeting on a slanted seam, as in the sprite's flush frames. They stay a little in while pressure builds.
    /// </summary>
    private void DrawHatch(DisposalDeviceBrush brush)
    {
        if (Model is not { } model)
            return;

        var closed = HatchAmount(model);
        if (closed <= 0f)
            return;

        var mouth = Layout.Mouth;
        var bandHeight = mouth.Height / HatchBands;
        for (var band = 0; band < HatchBands; band++)
        {
            var top = mouth.Top + band * bandHeight;
            var slant = (band - 2) * 5;
            var left = (int) MathF.Round(closed * (mouth.Width / 2f + slant));
            var right = (int) MathF.Round(closed * (mouth.Width / 2f - slant));
            brush.Rect(mouth.Left, top, left, bandHeight, DisposalDeviceArt.Steel);
            brush.Rect(mouth.Left + left - 1, top, 1, bandHeight, DisposalDeviceArt.SteelEdge);
            brush.Rect(mouth.Right - right, top, right, bandHeight, DisposalDeviceArt.Steel);
            brush.Rect(mouth.Right - right, top, 1, bandHeight, DisposalDeviceArt.SteelEdge);
            if (band == 0)
            {
                brush.Rect(mouth.Left, top, left - 1, 1, DisposalDeviceArt.SteelHigh);
                brush.Rect(mouth.Right - right + 1, top, right - 1, 1, DisposalDeviceArt.SteelHigh);
            }

            for (var seam = 9; seam < left - 2; seam += 10)
            {
                brush.Rect(mouth.Left + seam, top, 1, bandHeight, DisposalDeviceArt.SteelEdge);
            }

            for (var seam = 9; seam < right - 2; seam += 10)
            {
                brush.Rect(mouth.Right - seam, top, 1, bandHeight, DisposalDeviceArt.SteelEdge);
            }
        }
    }

    private float HatchAmount(DisposalDeviceModel model)
    {
        var charging = model.State != DisposalsPressureState.Ready;
        var age = model.SinceFlush;
        if (ReducedMotion || age >= HatchOpen)
            return charging ? HatchRest : 0f;

        if (age < HatchClose)
            return DisposalDeviceArt.Steps(age / HatchClose, 4);

        if (age < HatchHold)
            return 1f;

        return 1f - (1f - HatchRest) * DisposalDeviceArt.Steps((age - HatchHold) / (HatchOpen - HatchHold), 4);
    }
}
