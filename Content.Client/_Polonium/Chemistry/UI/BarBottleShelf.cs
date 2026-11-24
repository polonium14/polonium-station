using System.Numerics;
using Content.Client._Polonium.UserInterface.PixelUi;
using Content.Shared._Polonium.Chemistry;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client._Polonium.Chemistry.UI;

/// <summary>
/// The shelf of a dispenser that shows its containers: they stand on their necks in a felt rail, as on its sprite.
/// </summary>
public sealed class BarBottleShelf : Control
{
    public const int ArtHeight = BarDispenserArt.ShelfHeight + BarDispenserArt.RailHeight;

    /// <summary>
    /// The row on which the necks end, inside the felt.
    /// </summary>
    public const int Tip = Rail + 3;

    private const int ArtWidth = BarDispenserArt.BodyWidth;
    private const int Rail = BarDispenserArt.ShelfHeight;
    private const int SideRoom = 30;
    private const int MinPitch = 5;
    private const int MaxPitch = 14;
    private const int GrabHalf = 4;
    private const int BubbleStart = 9;
    private const int BubbleRise = 3;
    private const int BubbleGap = 5;

    private static readonly Angle UpsideDown = Angle.FromDegrees(180);
    private static readonly Color FeltShade = Color.FromHex("#013300");
    private static readonly Color Felt = Color.FromHex("#024300");
    private static readonly Color FeltLight = Color.FromHex("#025400");
    private static readonly Color Socket = Color.FromHex("#012400");
    private static readonly Color Bubble = Color.White.WithAlpha(0.75f);
    private static readonly Color SmallBubble = Color.White.WithAlpha(0.55f);

    private readonly BarSpriteProbe _probe;
    private readonly BarEntityPainter _painter;
    private IReadOnlyList<BarDrink> _drinks = Array.Empty<BarDrink>();
    private Vector2? _mouse;
    private bool _reduced;

    public BarDispenserSkinPrototype? Skin;

    /// <summary>
    /// The container the pointer is on, here or on its key.
    /// </summary>
    public BarDrink? Highlighted;

    /// <summary>
    /// The container under the pointer.
    /// </summary>
    public BarDrink? Hovered { get; private set; }

    public event Action<BarDrink>? OnPressed;

    public BarBottleShelf(BarSpriteProbe probe, BarEntityPainter painter)
    {
        _probe = probe;
        _painter = painter;

        MouseFilter = MouseFilterMode.Pass;
    }

    /// <summary>
    /// The column a container stands on, counted from the left edge of the shelf.
    /// </summary>
    public static int AxisOf(int index, int count)
    {
        // Too many containers for whole steps crowd together instead of leaving the shelf.
        var room = ArtWidth - SideRoom;
        var pitch = Math.Min(MaxPitch, room / Math.Max(1, count));
        var step = pitch >= MinPitch ? pitch : room / (float) count;
        return BarDispenserArt.Round(ArtWidth / 2f + (index - (count - 1) / 2f) * step);
    }

    /// <summary>
    /// Brings the shelf up to date with the dispenser.
    /// </summary>
    public void Advance(IReadOnlyList<BarDrink> drinks, bool reduced)
    {
        _drinks = drinks;
        _reduced = reduced;

        // Asked for here, a picture is measured before the shelf is drawn for the first time.
        foreach (var drink in drinks)
        {
            if (drink.Container is { } container)
                _probe.Get(container);
        }

        Hovered = Find();
        var shape = Hovered == null ? CursorShape.Arrow : CursorShape.Hand;
        if (DefaultCursorShape != shape)
            DefaultCursorShape = shape;

        this.SetLiveToolTip(Hovered?.Tip);
    }

    private BarDrink? Find()
    {
        if (_mouse is not { } mouse)
            return null;

        BarDrink? found = null;
        var nearest = GrabHalf + 1f;
        for (var i = 0; i < _drinks.Count; i++)
        {
            if (_drinks[i].Container is not { } container || _probe.Get(container) is not { Measured: true } info)
                continue;

            var distance = MathF.Abs(mouse.X - AxisOf(i, _drinks.Count));
            if (distance >= nearest || mouse.Y >= Tip || mouse.Y < Tip - info.Height)
                continue;

            nearest = distance;
            found = _drinks[i];
        }

        return found;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (Skin is not { } skin)
            return;

        var canvas = new PixelCanvas(handle, UIScale);
        var ink = BarDispenserArt.Ink;
        canvas.Rect(3, 9, ArtWidth - 6, Rail - 8, ink);
        canvas.Rect(4, 10, ArtWidth - 8, Rail - 9, skin.BodyDeep);
        canvas.Rect(0, 5, 5, Rail - 4, ink);
        canvas.Rect(1, 6, 3, Rail - 5, skin.Body);
        canvas.Rect(ArtWidth - 5, 5, 5, Rail - 4, ink);
        canvas.Rect(ArtWidth - 4, 6, 3, Rail - 5, skin.Body);

        for (var i = 0; i < _drinks.Count; i++)
        {
            DrawBottle(handle, canvas, _drinks[i], AxisOf(i, _drinks.Count));
        }

        canvas.Rect(0, Rail, ArtWidth, BarDispenserArt.RailHeight, ink);
        canvas.Rect(1, Rail + 1, ArtWidth - 2, BarDispenserArt.RailHeight - 2, Felt);
        canvas.Rect(1, Rail + 1, ArtWidth - 2, 1, FeltLight);
        canvas.Rect(1, Rail + BarDispenserArt.RailHeight - 2, ArtWidth - 2, 1, FeltShade);
        for (var i = 0; i < _drinks.Count; i++)
        {
            canvas.Rect(AxisOf(i, _drinks.Count) - 2, Rail + 1, 4, 2, Socket);
        }
    }

    private void DrawBottle(DrawingHandleScreen handle, PixelCanvas canvas, BarDrink drink, int axis)
    {
        if (drink.Container is not { } container)
            return;

        // A container that pours sinks into the felt by a pixel, one under the pointer rises by a pixel.
        var gurgles = !_reduced && drink.Gurgle > 0f;
        var info = _probe.Get(container);
        if (!info.Measured)
            return;

        var tip = Tip + (gurgles ? 1 : drink == Highlighted ? -1 : 0);
        _painter.Draw(handle, canvas, container, BarDispenserArt.Round(axis + info.Center), tip + info.Top, 1, UpsideDown);
        if (!gurgles)
            return;

        var frame = BarDispenserArt.Frame(BarDispenserModel.GurgleTime - drink.Gurgle);
        var top = tip - info.Height + 3;
        var bubble = tip - BubbleStart - frame * BubbleRise;
        if (bubble > top)
            canvas.Rect(axis - 1, bubble, 1, 1, Bubble);

        if (frame > 0 && bubble + BubbleGap > top)
            canvas.Rect(axis + 1, bubble + BubbleGap, 1, 1, SmallBubble);
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        _mouse = args.RelativePixelPosition / PixelCanvas.GetScale(UIScale);
    }

    protected override void MouseExited()
    {
        base.MouseExited();

        _mouse = null;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _mouse = args.RelativePixelPosition / PixelCanvas.GetScale(UIScale);
        if (Find() is not { } drink)
            return;

        OnPressed?.Invoke(drink);
        args.Handle();
    }
}
