using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client._Polonium.RichText;

public sealed class LinkLabel : Label
{
    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (TextMemory.Length == 0)
            return;

        Font font;
        if (FontOverride != null)
        {
            font = FontOverride;
        }
        else if (TryGetStyleProperty(StylePropertyFont, out Font? styleFont) && styleFont != null)
        {
            font = styleFont;
        }
        else
        {
            font = UserInterfaceManager.ThemeDefaults.LabelFont;
        }

        var color = FontColorOverride
            ?? (TryGetStyleProperty(StylePropertyFontColor, out Color styleColor) ? styleColor : Color.White);

        var height = font.GetHeight(UIScale);
        var vOffset = VAlign switch
        {
            VAlignMode.Center or VAlignMode.Fill => (PixelSize.Y - height) / 2f,
            VAlignMode.Bottom => PixelSize.Y - height,
            _ => 0f,
        };

        var thickness = Math.Max(1f, UIScale);
        var y = vOffset + font.GetAscent(UIScale) + font.GetDescent(UIScale);
        handle.DrawRect(new UIBox2(0, y - thickness, PixelSize.X, y), color);
    }
}
