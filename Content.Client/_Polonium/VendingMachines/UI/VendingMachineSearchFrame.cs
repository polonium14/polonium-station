using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.VendingMachines.UI;

/// <summary>
/// Pulsing amber frame drawn over a vending slot that matches the search query.
/// </summary>
public sealed partial class VendingMachineSearchFrame : Control
{
    [Dependency] private IGameTiming _timing = null!;

    private static readonly Color FrameColor = Color.FromHex("#ffb733");
    private static readonly Color CornerColor = Color.FromHex("#ffe2a8");

    private const float AppearDuration = 0.22f;
    private const float PulsePeriod = 1.6f;
    private const int GlowLayers = 6;
    private const float GlowAlpha = 0.3f;
    private const float BorderThickness = 2f;
    private const float CornerThickness = 3f;
    private const float CornerLength = 16f;

    private float _appearElapsed;

    public VendingMachineSearchFrame()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Ignore;
    }

    /// <summary>
    /// Replays the appear animation.
    /// </summary>
    public void Restart()
    {
        _appearElapsed = 0f;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_appearElapsed < AppearDuration)
            _appearElapsed += args.DeltaSeconds;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var appear = Math.Clamp(_appearElapsed / AppearDuration, 0f, 1f);
        appear = 1f - MathF.Pow(1f - appear, 3f);

        var phase = (float) (_timing.RealTime.TotalSeconds % PulsePeriod / PulsePeriod);
        var pulse = 0.5f + 0.5f * MathF.Sin(phase * MathF.Tau);

        var scale = UIScale;
        var box = UIBox2.FromDimensions(Vector2.Zero, PixelSize);

        for (var i = 0; i < GlowLayers; i++)
        {
            var falloff = 1f - (float) i / GlowLayers;
            var alpha = GlowAlpha * falloff * falloff * (0.55f + 0.45f * pulse) * appear;
            var inset = (BorderThickness + i) * scale;
            var layer = new UIBox2(box.Left + inset, box.Top + inset, box.Right - inset, box.Bottom - inset);
            DrawOutline(handle, layer, scale, FrameColor.WithAlpha(alpha));
        }

        DrawOutline(handle, box, BorderThickness * scale, FrameColor.WithAlpha((0.7f + 0.3f * pulse) * appear));

        var length = CornerLength * scale * appear;
        var thickness = CornerThickness * scale;
        var corner = CornerColor.WithAlpha(appear);

        DrawCorner(handle, box.TopLeft, 1, 1, length, thickness, corner);
        DrawCorner(handle, box.TopRight, -1, 1, length, thickness, corner);
        DrawCorner(handle, box.BottomLeft, 1, -1, length, thickness, corner);
        DrawCorner(handle, box.BottomRight, -1, -1, length, thickness, corner);
    }

    private static void DrawOutline(DrawingHandleScreen handle, UIBox2 box, float thickness, Color color)
    {
        handle.DrawRect(new UIBox2(box.Left, box.Top, box.Right, box.Top + thickness), color);
        handle.DrawRect(new UIBox2(box.Left, box.Bottom - thickness, box.Right, box.Bottom), color);
        handle.DrawRect(new UIBox2(box.Left, box.Top + thickness, box.Left + thickness, box.Bottom - thickness), color);
        handle.DrawRect(new UIBox2(box.Right - thickness, box.Top + thickness, box.Right, box.Bottom - thickness), color);
    }

    private static void DrawCorner(DrawingHandleScreen handle, Vector2 origin, int dirX, int dirY, float length, float thickness, Color color)
    {
        handle.DrawRect(BoxBetween(origin, origin + new Vector2(dirX * length, dirY * thickness)), color);
        handle.DrawRect(BoxBetween(origin + new Vector2(0, dirY * thickness), origin + new Vector2(dirX * thickness, dirY * length)), color);
    }

    private static UIBox2 BoxBetween(Vector2 a, Vector2 b)
    {
        return new UIBox2(Vector2.Min(a, b), Vector2.Max(a, b));
    }
}
