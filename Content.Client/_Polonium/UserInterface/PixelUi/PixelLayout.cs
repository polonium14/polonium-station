namespace Content.Client._Polonium.UserInterface.PixelUi;

/// <summary>
/// Places the parts of a pixel-art window so that each covers a whole number of screen pixels.
/// </summary>
public static class PixelLayout
{
    // The engine cuts a control's position and size down to whole screen pixels. Without a little slack
    // float error costs a row or a column at interface scales such as 125 %.
    private const float Slack = 0.25f;

    /// <summary>
    /// A box given in art pixels, placed so that it covers exactly those pixels on the screen.
    /// </summary>
    public static UIBox2 Box(float uiScale, float left, float top, float width, float height)
    {
        var scale = PixelCanvas.GetScale(uiScale);
        return UIBox2.FromDimensions(
            Snap(left * scale) / uiScale,
            Snap(top * scale) / uiScale,
            Snap(width * scale) / uiScale,
            Snap(height * scale) / uiScale);
    }

    /// <inheritdoc cref="Box(float,float,float,float,float)"/>
    public static UIBox2 Box(float uiScale, UIBox2i art)
    {
        return Box(uiScale, art.Left, art.Top, art.Width, art.Height);
    }

    private static float Snap(float pixels)
    {
        return pixels < 0f ? pixels - Slack : pixels + Slack;
    }
}
