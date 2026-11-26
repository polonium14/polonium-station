using System.Numerics;
using System.Runtime.InteropServices;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Utility;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._Polonium.Smoking;

/// <summary>
/// Draws every <see cref="SmokeRibbon"/> as one soft strip: thin and dense at the source, wider and fainter with age.
/// </summary>
public sealed partial class SmokeWispOverlay : Overlay
{
    [Dependency] private IResourceCache _resources = default!;

    /// <summary>
    /// Sampled across the strip's width, so the edges fade out and the middle stays dense.
    /// </summary>
    private static readonly ResPath SoftTexture = new("/Textures/Effects/HeatBlur/soft_circle.png");

    private static readonly Color SmokeColor = Color.FromHex("#d8d8d8");

    private const float Opacity = 0.5f;
    private const float WidthAtTip = 0.02f;
    private const float WidthGrowth = 0.14f;

    /// <summary>
    /// Seconds over which the strip fades in from the source, so it does not start with a hard edge.
    /// </summary>
    private const float FadeIn = 0.06f;

    /// <summary>
    /// Ribbons with no end within this many tiles of the view are skipped.
    /// </summary>
    private const float CullMargin = 3f;

    private readonly SmokeWispSystem _system;
    private readonly Texture _texture;
    private readonly List<DrawVertexUV2DColor> _vertices = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    public SmokeWispOverlay(SmokeWispSystem system)
    {
        IoCManager.InjectDependencies(this);
        ZIndex = (int) DrawDepth.Effects;
        _system = system;
        _texture = _resources.GetResource<TextureResource>(SoftTexture).Texture;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var bounds = args.WorldAABB.Enlarged(CullMargin);

        foreach (var ribbon in _system.Ribbons)
        {
            var points = ribbon.Points;
            if (ribbon.MapId != args.MapId || points.Count == 0)
                continue;

            var count = points.Count + (ribbon.Head != null ? 1 : 0);
            if (count < 2
                || !bounds.Contains(points[0].Position) && !bounds.Contains(ribbon.Head ?? points[^1].Position))
                continue;

            AddRibbon(ribbon, count);
        }

        if (_vertices.Count == 0)
            return;

        args.WorldHandle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _texture, CollectionsMarshal.AsSpan(_vertices));
        _vertices.Clear();
    }

    private void AddRibbon(SmokeRibbon ribbon, int count)
    {
        var normal = Vector2.UnitX;
        Vector2 prevLeft = default, prevRight = default;
        Color prevColor = default;

        for (var i = 0; i < count; i++)
        {
            var (position, age) = Get(ribbon, i);

            // Smooth tangent from the neighbours; standing still can stack points, then keep the last normal.
            var tangent = Get(ribbon, Math.Min(i + 1, count - 1)).Position - Get(ribbon, Math.Max(i - 1, 0)).Position;
            var length = tangent.Length();
            if (length > 0.0001f)
                normal = new Vector2(-tangent.Y, tangent.X) / length;

            var life = Math.Clamp(age / SmokeWispSystem.Lifetime, 0f, 1f);
            var halfWidth = (WidthAtTip + WidthGrowth * age) * 0.5f;
            var alpha = Opacity * MathF.Pow(1f - life, 1.5f) * Math.Min(age / FadeIn, 1f);
            var color = Color.FromSrgb(SmokeColor.WithAlpha(alpha));

            var left = position + normal * halfWidth;
            var right = position - normal * halfWidth;

            if (i > 0)
            {
                _vertices.Add(new DrawVertexUV2DColor(prevLeft, new Vector2(0f, 0.5f), prevColor));
                _vertices.Add(new DrawVertexUV2DColor(prevRight, new Vector2(1f, 0.5f), prevColor));
                _vertices.Add(new DrawVertexUV2DColor(left, new Vector2(0f, 0.5f), color));
                _vertices.Add(new DrawVertexUV2DColor(prevRight, new Vector2(1f, 0.5f), prevColor));
                _vertices.Add(new DrawVertexUV2DColor(right, new Vector2(1f, 0.5f), color));
                _vertices.Add(new DrawVertexUV2DColor(left, new Vector2(0f, 0.5f), color));
            }

            prevLeft = left;
            prevRight = right;
            prevColor = color;
        }
    }

    /// <summary>
    /// Point <paramref name="index"/> of the ribbon from oldest to newest, the live source last.
    /// </summary>
    private static (Vector2 Position, float Age) Get(SmokeRibbon ribbon, int index)
    {
        var points = ribbon.Points;
        if (index < points.Count)
            return (points[index].Position, points[index].Age);

        return (ribbon.Head!.Value, 0f);
    }
}
