using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Shitmed.Medical.Surgery;

/// <summary>
/// Thin overlay drawn on top of a <see cref="SurgeryStepButton"/> that fills left-to-right with
/// a translucent green as the DoAfter for that step progresses, mirroring the color language of
/// the world-space DoAfter progress bar (see Content.Client/DoAfter/DoAfterOverlay.cs) without
/// needing a shader - it's just a rectangle sized to <see cref="Progress"/> drawn every frame.
/// </summary>
public sealed class SurgeryStepProgressFill : Control
{
    private static readonly Color FillColor = new(0.2f, 0.8f, 0.2f, 0.45f);

    /// <summary>
    /// 0 to 1 progress of the active DoAfter for this step, or 0/hidden if none is active.
    /// </summary>
    public float Progress { get; set; }

    public SurgeryStepProgressFill()
    {
        MouseFilter = MouseFilterMode.Ignore;
        Visible = false;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (!Visible || Progress <= 0f)
            return;

        var width = PixelWidth * Math.Clamp(Progress, 0f, 1f);
        if (width <= 0f)
            return;

        handle.DrawRect(new UIBox2(0, 0, width, PixelHeight), FillColor);
    }
}
