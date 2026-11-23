using Content.Client._Polonium.UserInterface.PixelUi;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Polonium.Kitchen.UI;

/// <summary>
/// A sign printed on the blender's body next to the selector knob. Clicking it turns the knob to that position.
/// </summary>
public sealed class GrinderMark : BaseButton
{
    private const int TextRise = 4;

    public Texture? Atlas;
    public UIBox2? Icon;
    public PixelFont Font;

    /// <summary>
    /// Written in place of a picture when <see cref="Icon"/> is not set.
    /// </summary>
    public string Text = string.Empty;

    public Color Tint = Color.White;

    public GrinderMark()
    {
        DefaultCursorShape = CursorShape.Hand;
        MuteSounds = true;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var canvas = new PixelCanvas(handle, UIScale);
        var centerX = PixelSize.X / canvas.Scale / 2;
        var centerY = PixelSize.Y / canvas.Scale / 2;
        if (Icon is { } icon && Atlas != null)
            canvas.Region(Atlas, icon, centerX - ((int) icon.Width - 1) / 2, centerY - ((int) icon.Height - 1) / 2, Tint);
        else if (Text.Length > 0 && Font.Font != null)
            canvas.Text(Font, centerX - (Font.Measure(Text) - 2) / 2, centerY - TextRise, Text, Tint);
    }
}
