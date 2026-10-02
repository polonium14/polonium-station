using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Polonium.UserInterface;

/// <summary>
/// A tab container that can glow around the header of one tab until the player opens that tab.
/// </summary>
public sealed class GlowTabContainer : TabContainer
{
    public Control? GlowTab { get; set; }

    public GlowTabContainer()
    {
        OnTabChanged += index =>
        {
            if (GetChild(index) == GlowTab)
                GlowTab = null;
        };
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (GlowTab is not { } tab || tab.Parent != this)
            return;

        if (TryGetTabBox(tab.GetPositionInParent(), out var box))
            UiGlow.DrawFrame(handle, box, UIScale, UiGlow.Strength());
    }

    // The engine keeps the laid out tab headers to itself, so this repeats its layout.
    private bool TryGetTabBox(int tab, out UIBox2 result)
    {
        result = default;

        if (!TabsVisible)
            return false;

        if (!TryGetStyleProperty<Font>("font", out var font))
            font = UserInterfaceManager.ThemeDefaults.DefaultFont;

        TryGetStyleProperty<StyleBox>(StylePropertyTabStyleBox, out var active);
        TryGetStyleProperty<StyleBox>(StylePropertyTabStyleBoxInactive, out var inactive);

        var left = 0f;
        var top = 0f;
        var rowHeight = 0f;

        for (var i = 0; i < ChildCount; i++)
        {
            if (!GetTabVisible(i))
                continue;

            var width = 0;
            foreach (var rune in GetActualTabTitle(i).EnumerateRunes())
            {
                if (font.TryGetCharMetrics(rune, UIScale, out var metrics))
                    width += metrics.Advance;
            }

            var style = i == CurrentTab ? active : inactive;
            var size = new Vector2(width, font.GetHeight(UIScale));
            var bounds = GetBounds(style, new Vector2(left, top), size);

            if (left + bounds.Width > PixelWidth)
            {
                left = 0;
                top += rowHeight;
                rowHeight = 0;
                bounds = GetBounds(style, new Vector2(left, top), size);
            }

            if (i == tab)
            {
                result = bounds;
                return true;
            }

            left += bounds.Width;
            rowHeight = Math.Max(rowHeight, bounds.Height);
        }

        return false;
    }

    private UIBox2 GetBounds(StyleBox? style, Vector2 topLeft, Vector2 size)
    {
        return style?.GetEnvelopBox(topLeft, size, UIScale) ?? UIBox2.FromDimensions(topLeft, size);
    }
}
