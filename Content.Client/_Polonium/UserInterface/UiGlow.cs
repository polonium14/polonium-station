using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.UserInterface;

/// <summary>
/// The tutorial item highlight, redone for ui textures. Controls get no post shaders, so the halo
/// is the texture stamped around itself in white and the pulsing fill is one more white pass on top.
/// Only works on textures that are white and get their color from a tint, which the wire hacking
/// ones all are.
/// </summary>
public static class UiGlow
{
    // same numbers as the item variant of the world shader
    private const float Speed = 3.4f;
    private const float Min = 0.18f;
    private const float Max = 0.7f;
    private const float Width = 1.5f;

    private static readonly Vector2[] Directions =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
        new(0.7f, 0.7f), new(-0.7f, 0.7f), new(0.7f, -0.7f), new(-0.7f, -0.7f),
    };

    public static float Strength()
    {
        var time = (float) IoCManager.Resolve<IGameTiming>().RealTime.TotalSeconds;
        var pulse = (MathF.Sin(time * Speed) + 1f) * 0.5f;
        return Min + (Max - Min) * pulse;
    }

    /// <summary>Draw before the texture itself, so only the part sticking out stays visible.</summary>
    public static void DrawHalo(DrawingHandleScreen handle, Texture texture, UIBox2 rect, float uiScale, float strength)
    {
        var inner = Color.White.WithAlpha(strength);
        var outer = Color.White.WithAlpha(strength * 0.4f);

        foreach (var dir in Directions)
        {
            handle.DrawTextureRect(texture, rect.Translated(dir * Width * 2f * uiScale), outer);
            handle.DrawTextureRect(texture, rect.Translated(dir * Width * uiScale), inner);
        }
    }

    /// <summary>Draw after the texture, lifts it towards white the way the shader fill does.</summary>
    public static void DrawFill(DrawingHandleScreen handle, Texture texture, UIBox2 rect, float strength)
    {
        handle.DrawTextureRect(texture, rect, Color.White.WithAlpha(strength * 0.6f));
    }

    /// <summary>
    /// A glow around a whole control, for fields and buttons that have no single texture to trace:
    /// rings that fade as they spread out, and a faint wash over the control itself.
    /// </summary>
    public static void DrawFrame(DrawingHandleScreen handle, UIBox2 box, float uiScale, float strength)
    {
        for (var i = 1; i <= 4; i++)
        {
            var d = i * 1.5f * uiScale;
            var ring = new UIBox2(box.Left - d, box.Top - d, box.Right + d, box.Bottom + d);
            handle.DrawRect(ring, Color.White.WithAlpha(strength * (1.1f - i * 0.25f)), filled: false);
        }

        handle.DrawRect(box, Color.White.WithAlpha(strength * 0.15f));
    }
}

/// <summary>
/// Sits over the whole ui and draws a frame glow around every target, wherever the target is right now.
/// Ignores the mouse, so everything underneath still gets clicked.
/// </summary>
public sealed class UiGlowFrames : Control
{
    private readonly List<Control> _targets = new();

    public UiGlowFrames()
    {
        MouseFilter = MouseFilterMode.Ignore;
    }

    public void SetTargets(IEnumerable<Control> targets)
    {
        _targets.Clear();
        _targets.AddRange(targets);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (_targets.Count == 0)
            return;

        var strength = UiGlow.Strength();
        foreach (var target in _targets)
        {
            if (!target.VisibleInTree)
                continue;

            Vector2 topLeft = target.GlobalPixelPosition - GlobalPixelPosition;
            Vector2 size = target.PixelSize;
            UiGlow.DrawFrame(handle, UIBox2.FromDimensions(topLeft, size), UIScale, strength);
        }
    }
}

/// <summary>
/// A texture rect that can glow. The tint is applied to the texture only, not through Modulate,
/// because Modulate would dye the white halo too.
/// </summary>
public sealed class GlowTextureRect : TextureRect
{
    public bool Glow { get; set; }

    public Color Tint { get; set; } = Color.White;

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (Texture is not { } texture)
            return;

        var rect = GetDrawDimensions(texture);
        var strength = Glow ? UiGlow.Strength() : 0f;

        if (Glow)
            UiGlow.DrawHalo(handle, texture, rect, UIScale, strength);

        handle.DrawTextureRect(texture, rect, Tint);

        if (Glow)
            UiGlow.DrawFill(handle, texture, rect, strength);
    }
}
