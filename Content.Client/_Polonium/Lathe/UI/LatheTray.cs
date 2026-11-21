using Content.Client._Polonium.UserInterface.PixelUi;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._Polonium.Lathe.UI;

/// <summary>
/// The output tray: a finished item rolls out of the machine here and fades as it is taken away.
/// </summary>
public sealed class LatheTray : Control
{
    public const int ArtWidth = 114;
    public const int ArtHeight = 22;

    private const float Delay = 0.16f;
    private const float RollTime = 1.24f;
    private const float RollSpeed = 1.6f;
    private const float RollReach = 0.55f;
    private const float FadeStart = 0.75f;
    private const int Floor = 2;

    private readonly LatheConsoleModel _model;
    private readonly LatheIconCache _icons;
    private readonly PixelFont _font;
    private readonly string _label;
    private readonly int _labelWidth;

    public LatheTray(LatheConsoleModel model, LatheIconCache icons, PixelFont font)
    {
        _model = model;
        _icons = icons;
        _font = font;
        _label = Loc.GetString("polonium-lathe-tray-label").ToUpperInvariant();
        _labelWidth = _font.Measure(_label) - 1;
        MouseFilter = MouseFilterMode.Ignore;
        RectClipContent = true;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var canvas = new PixelCanvas(handle, UIScale);
        foreach (var output in _model.Outputs)
        {
            var elapsed = (float) (_model.Now - output.Time).TotalSeconds - Delay;
            var icon = _icons.Get(output.Recipe, true);
            if (elapsed < 0f || icon.Texture is not { } texture)
                continue;

            var part = elapsed / RollTime;
            var roll = _model.ReducedMotion ? 1f : MathF.Min(1f, part * RollSpeed);
            var alpha = part > FadeStart ? Math.Clamp((1f - part) / (1f - FadeStart), 0f, 1f) : 1f;
            var bounds = icon.Bounds;
            var x = (int) MathF.Round(-bounds.Width + roll * (ArtWidth * RollReach + bounds.Width));
            canvas.Region(texture, bounds, x, ArtHeight - Floor - bounds.Height, Color.White.WithAlpha(alpha));
        }

        canvas.Text(_font, ArtWidth - 3 - _labelWidth, ArtHeight - 1 - 9, _label, LathePalette.TrayLabel);
    }
}
