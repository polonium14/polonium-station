using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.UserInterface.Controls;

/// <summary>
/// A scroll container whose rows fade out at the top and bottom edge while there is more to scroll that way, so the
/// list shows it goes on. It fades the rows themselves, so it works on any background.
/// </summary>
/// <remarks>
/// Fades the children of its single content control, so put the rows in one box inside it.
/// </remarks>
public sealed class FadingScrollContainer : ScrollContainer
{
    /// <summary>
    /// How far from an edge, in UI units, a row starts to fade.
    /// </summary>
    public float FadeHeight { get; set; } = 28f;

    /// <summary>
    /// How see-through a row right at the edge gets, 0 to 1.
    /// </summary>
    public float MinAlpha { get; set; } = 0.2f;

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        Control? content = null;
        foreach (var child in Children)
        {
            if (child is not ScrollBar)
            {
                content = child;
                break;
            }
        }

        if (content == null)
            return;

        var moreAbove = VScroll > 0.5f;
        var moreBelow = VScroll + Height < content.Height - 0.5f;
        var top = GlobalPosition.Y;

        foreach (var row in content.Children)
        {
            var center = row.GlobalPosition.Y - top + row.Height / 2f;
            var alpha = 1f;
            if (moreAbove)
                alpha = MathF.Min(alpha, center / FadeHeight);
            if (moreBelow)
                alpha = MathF.Min(alpha, (Height - center) / FadeHeight);

            var color = Color.White.WithAlpha(Math.Clamp(alpha, MinAlpha, 1f));
            if (row.Modulate != color)
                row.Modulate = color;
        }
    }
}
