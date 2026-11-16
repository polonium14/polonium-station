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

        var scroll = VScroll;
        // How strongly each edge fades: in full while there is a whole fade's worth left to scroll that way, easing
        // off over the last stretch, so reaching the end does not light the edge row up at once.
        var topStrength = Math.Clamp(scroll / FadeHeight, 0f, 1f);
        var bottomStrength = Math.Clamp((content.Height - Height - scroll) / FadeHeight, 0f, 1f);

        foreach (var row in content.Children)
        {
            // From the row's place in the list and the current scroll, not its global position: that one is only
            // updated at the next arrange, so it lags a frame behind while scrolling and the alpha jumps.
            var center = row.Position.Y + row.Height / 2f - scroll;
            var top = Math.Clamp(center / FadeHeight, MinAlpha, 1f);
            var bottom = Math.Clamp((Height - center) / FadeHeight, MinAlpha, 1f);
            var alpha = MathF.Min(
                MathHelper.Lerp(1f, top, topStrength),
                MathHelper.Lerp(1f, bottom, bottomStrength));

            var color = Color.White.WithAlpha(alpha);
            if (row.Modulate != color)
                row.Modulate = color;
        }
    }
}
