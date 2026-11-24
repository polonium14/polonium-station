using System.Numerics;
using Content.Client._Polonium.Lathe.UI;
using Content.Client._Polonium.UserInterface.PixelUi;
using Content.Shared._Polonium.Chemistry;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Polonium.Chemistry.UI;

/// <summary>
/// A key that pours one drink: a cap in the drink's colour, its name and a gauge of what is left in the container.
/// </summary>
public sealed class BarDrinkKey : BaseButton
{
    private const int ArtWidth = BarDispenserArt.KeyWidth;
    private const int ArtHeight = BarDispenserArt.KeyHeight;
    private const int FaceHeight = ArtHeight - 3;
    private const int CapWidth = 7;
    private const int TextLeft = 11;
    private const int TextRoom = ArtWidth - 17;
    private const int GaugeHeight = ArtHeight - 5;
    private const float CapShine = 0.35f;
    private const string Ellipsis = "…";

    private static readonly Color Print = Color.FromHex("#1d1d1d");
    private static readonly Color FadedPrint = Color.FromHex("#8a8272");
    private static readonly Color EmptyCap = Color.FromHex("#4a4a4a");
    private static readonly Color EmptyCapShine = Color.FromHex("#5c5c5c");
    private static readonly Color GaugeSlot = Color.FromHex("#3a3a3a");

    private readonly LatheScreenFont _font;
    private string _shown = string.Empty;
    private string? _fitted;
    private int _fittedScale;

    public BarDrink? Drink;
    public BarDispenserSkinPrototype? Skin;

    /// <summary>
    /// Whether a press takes the container out instead of pouring from it.
    /// </summary>
    public bool Service;

    /// <summary>
    /// Whether the pointer is on the key or on the container it stands for.
    /// </summary>
    public bool Highlighted;

    public BarDrinkKey(LatheScreenFont font)
    {
        _font = font;
        DefaultCursorShape = CursorShape.Hand;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (Drink is not { } drink || Skin is not { } skin)
            return;

        var canvas = new PixelCanvas(handle, UIScale);
        var down = DrawMode == DrawModeEnum.Pressed;
        var empty = drink.Quantity <= 0f && !Service;
        var top = down ? 2 : 1;

        canvas.Rect(0, 0, ArtWidth, ArtHeight, Service ? BarDispenserArt.Amber : BarDispenserArt.Ink);
        canvas.Rect(1, top, ArtWidth - 2, FaceHeight, down ? skin.KeyPressed : Highlighted ? skin.KeyHover : skin.Key);
        if (!down)
            canvas.Rect(1, ArtHeight - 2, ArtWidth - 2, 1, skin.KeyShade);

        canvas.Rect(1, top, CapWidth, FaceHeight, empty ? EmptyCap : drink.Color);
        canvas.Rect(1, top, CapWidth, 1, empty ? EmptyCapShine : BarDispenserArt.Lighten(drink.Color, CapShine));
        canvas.Rect(1 + CapWidth, top, 1, FaceHeight, BarDispenserArt.Ink);

        DrawName(handle, canvas.Scale, drink.Name, top, empty ? FadedPrint : Print);
        if (Service)
            DrawEjectMark(canvas, ArtWidth - 7, top + 2);
        else
            DrawGauge(canvas, drink, ArtWidth - 6, top + 1);
    }

    private void DrawName(DrawingHandleScreen handle, int scale, string name, int top, Color color)
    {
        var face = _font.PixelFace(scale);
        if (_fitted != name || _fittedScale != scale)
        {
            _fitted = name;
            _fittedScale = scale;
            var room = TextRoom * scale;
            _shown = face.Width(name) <= room
                ? name
                : string.Concat(name.AsSpan(0, face.Fit(name, room - face.Width(Ellipsis))).TrimEnd(), Ellipsis);
        }

        var line = top * scale + (FaceHeight * scale - face.Line * face.Dot) / 2;
        handle.DrawString(face.Font.Font, new Vector2(TextLeft * scale, line), _shown, face.DrawScale, color);
    }

    /// <summary>
    /// The level left in the container: a dark slot filled from below in the drink's colour.
    /// </summary>
    private static void DrawGauge(PixelCanvas canvas, BarDrink drink, int x, int y)
    {
        var room = GaugeHeight - 2;
        var share = drink.Capacity > 0f ? Math.Clamp(drink.Quantity / drink.Capacity, 0f, 1f) : 1f;
        var fill = drink.Quantity <= 0f ? 0 : Math.Max(1, BarDispenserArt.Round(room * share));
        canvas.Rect(x, y, 4, GaugeHeight, BarDispenserArt.Ink);
        canvas.Rect(x + 1, y + 1, 2, room, GaugeSlot);
        canvas.Rect(x + 1, y + 1 + room - fill, 2, fill, drink.Color);
    }

    private static void DrawEjectMark(PixelCanvas canvas, int x, int y)
    {
        canvas.Rect(x + 2, y, 1, 1, Print);
        canvas.Rect(x + 1, y + 1, 3, 1, Print);
        canvas.Rect(x, y + 2, 5, 1, Print);
        canvas.Rect(x, y + 4, 5, 1, Print);
    }
}
