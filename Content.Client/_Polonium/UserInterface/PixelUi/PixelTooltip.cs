using Robust.Client.UserInterface;

namespace Content.Client._Polonium.UserInterface.PixelUi;

public static class PixelTooltip
{
    /// <summary>
    /// Gives a control a new tooltip text. The engine goes on showing the old text for as long as the mouse rests
    /// on the control, so a tooltip that is up is taken down and comes back with the new text.
    /// </summary>
    public static void SetLiveToolTip(this Control control, string? text)
    {
        if (control.ToolTip == text)
            return;

        control.ToolTip = text;
        control.HideTooltip();
    }
}
