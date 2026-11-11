using System.Numerics;
using Content.Shared._Polonium.Crosshairs;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client._Polonium.Crosshairs;

/// <summary>
/// Draws the crosshair picked by <see cref="CombatCrosshairSystem"/> at the cursor and hides the system cursor under it.
/// Gun crosshairs are sized to the spread of the next shot.
/// </summary>
public sealed partial class CombatCrosshairOverlay : Overlay
{
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IPlayerManager _player = default!;

    private static readonly Color OutlineColor = Color.Black.WithAlpha(0.6f);
    private static readonly Vector2[] Axes = [new(1, 0), new(-1, 0), new(0, 1), new(0, -1)];
    private static readonly Vector2[] Diagonals = [new(-1, -1), new(1, -1), new(-1, 1), new(1, 1)];
    private static readonly Vector2i[] PixelNeighbours = [new(1, 0), new(-1, 0), new(0, 1), new(0, -1)];

    private const float MaxUiScale = 1.25f;
    private const float BaseMinGap = 10f;
    private const float BaseTickLength = 6f;
    private const float BaseThickness = 2f;
    private const float SpreadKnee = 35f;
    private const float SpreadFalloff = 70f;
    private const int CloudLayers = 6;
    private const int PixelArm = 2;
    private const float ChevronApex = 14f;
    private const float ChevronEnd = 8f;
    private const float ChevronHalfWidth = 4.5f;
    private const float ChevronWidth = 2.2f;
    private const float RailLength = 16f;
    private const int MaxArcSegments = 64;

    private readonly CombatCrosshairSystem _system;
    private readonly SharedTransformSystem _transform;

    private Color _color = Color.White;
    private float _scale = 1f;
    private bool _outline = true;
    private ICursor? _blankCursor;
    private Control? _hiddenCursorViewport;

    // The client sandbox bans stackalloc, so vertex and cell scratch space lives here.
    private readonly Vector2[] _vertices = new Vector2[(MaxArcSegments + 1) * 2];
    private readonly Vector2i[] _cells = new Vector2i[1 + 4 * PixelArm];
    private readonly Vector2i[] _outlineCells = new Vector2i[(1 + 4 * PixelArm) * 4];

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public CombatCrosshairOverlay(IDependencyCollection deps, CombatCrosshairSystem system)
    {
        deps.InjectDependencies(this);
        _system = system;
        _transform = _entities.System<SharedTransformSystem>();

        _cfg.OnValueChanged(CCVars.CrosshairColor, OnColorChanged, true);
        _cfg.OnValueChanged(CCVars.CrosshairScale, OnScaleChanged, true);
        _cfg.OnValueChanged(CCVars.CrosshairOutline, OnOutlineChanged, true);
    }

    private void OnColorChanged(string value)
    {
        _color = Color.TryFromHex(value, out var color) ? color : Color.White;
    }

    private void OnScaleChanged(float value)
    {
        _scale = value;
    }

    private void OnOutlineChanged(bool value)
    {
        _outline = value;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (_system.Crosshair != null)
            return true;

        UpdateCursor(null);
        return false;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_system.Crosshair is not { } crosshair || args.ViewportControl is not Control viewport)
            return;

        var mouse = _input.MouseScreenPosition;
        if (_eye.PixelToMap(mouse).MapId != args.MapId || !viewport.GlobalPixelRect.Contains((Vector2i) mouse.Position))
            return;

        UpdateCursor(viewport);

        var originEntity = _system.Gun is { } gun && _entities.HasComponent<GunUseGunOriginComponent>(gun) ? gun : _player.LocalEntity;
        var origin = originEntity is { } ent ? _eye.MapToScreen(_transform.GetMapCoordinates(ent)).Position : mouse.Position;
        var toMouse = mouse.Position - origin;
        var sight = new Sight(
            mouse.Position,
            toMouse.Length(),
            MathF.Atan2(toMouse.Y, toMouse.X),
            _system.Spread,
            MathF.Min(viewport.UIScale, MaxUiScale) * _scale);

        var handle = args.ScreenHandle;
        if (crosshair.FaceShooter && crosshair.Shape != CrosshairShape.PixelCross)
            handle.SetTransform(Matrix3x2.CreateRotation(sight.Angle, sight.Center));

        if (_outline)
            DrawShape(handle, crosshair.Shape, sight, OutlineColor, 1f);

        DrawShape(handle, crosshair.Shape, sight, _color, 0f);
        handle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawShape(DrawingHandleScreen handle, CrosshairShape shape, in Sight sight, Color color, float grow)
    {
        switch (shape)
        {
            case CrosshairShape.Cross:
                DrawTicks(handle, sight, color, grow, true);
                break;
            case CrosshairShape.TCross:
                DrawTicks(handle, sight, color, grow, false);
                break;
            case CrosshairShape.Corners:
                DrawCorners(handle, sight, color, grow);
                break;
            case CrosshairShape.Grid:
                DrawGrid(handle, sight, color, grow);
                break;
            case CrosshairShape.Ring:
                DrawArc(handle, sight.Center, MathF.Max(sight.Gap, sight.Thickness), 0f, MathF.Tau, sight.Thickness * 0.7f + grow * 2, color);
                DrawDot(handle, sight, color, grow);
                break;
            case CrosshairShape.DashedRing:
                DrawDashedRing(handle, sight, color, grow);
                break;
            case CrosshairShape.Arcs:
                DrawDiagonalArcs(handle, sight, color, grow);
                break;
            case CrosshairShape.Cloud:
                DrawCloud(handle, sight, color, grow);
                break;
            case CrosshairShape.Rails:
                DrawRails(handle, sight, color, grow);
                break;
            case CrosshairShape.PixelCross:
                DrawPixelCross(handle, sight, color, grow);
                break;
            case CrosshairShape.Chevrons:
                DrawChevrons(handle, sight, color, grow);
                break;
        }
    }

    private void DrawChevrons(DrawingHandleScreen handle, in Sight sight, Color color, float grow)
    {
        var apex = ChevronApex * sight.Scale;
        var end = ChevronEnd * sight.Scale;
        var halfWidth = ChevronHalfWidth * sight.Scale;
        var width = ChevronWidth * sight.Scale;

        var slope = halfWidth / (apex - end);
        var sin = halfWidth / MathF.Sqrt(halfWidth * halfWidth + (apex - end) * (apex - end));
        var outerApex = apex + grow / sin;
        var innerApex = apex - (width + grow) / sin;
        var cut = end - grow;
        var outerSide = slope * (outerApex - cut);
        var innerSide = MathF.Max(0f, slope * (innerApex - cut));

        foreach (var dir in Axes)
        {
            var side = new Vector2(-dir.Y, dir.X);
            var c = sight.Center;
            _vertices[0] = c + dir * cut - side * outerSide;
            _vertices[1] = c + dir * cut - side * innerSide;
            _vertices[2] = c + dir * outerApex;
            _vertices[3] = c + dir * innerApex;
            _vertices[4] = c + dir * cut + side * outerSide;
            _vertices[5] = c + dir * cut + side * innerSide;
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, new ReadOnlySpan<Vector2>(_vertices, 0, 6), color);
        }
    }

    private static void DrawTicks(DrawingHandleScreen handle, in Sight sight, Color color, float grow, bool top)
    {
        var c = sight.Center;
        var t = sight.Thickness;
        var l = sight.TickLength;
        var g = sight.Gap;

        DrawBox(handle, c + new Vector2(-t, -t) / 2, c + new Vector2(t, t) / 2, grow, color);
        DrawBox(handle, c + new Vector2(g, -t / 2), c + new Vector2(g + l, t / 2), grow, color);
        DrawBox(handle, c + new Vector2(-g - l, -t / 2), c + new Vector2(-g, t / 2), grow, color);
        DrawBox(handle, c + new Vector2(-t / 2, g), c + new Vector2(t / 2, g + l), grow, color);
        if (top)
            DrawBox(handle, c + new Vector2(-t / 2, -g - l), c + new Vector2(t / 2, -g), grow, color);
    }

    private static void DrawCorners(DrawingHandleScreen handle, in Sight sight, Color color, float grow)
    {
        var c = sight.Center;
        var b = sight.Gap + 2f * sight.Scale;
        var arm = MathF.Min(sight.TickLength, b * 0.8f);
        var w = sight.Thickness * 0.8f;

        foreach (var d in Diagonals)
        {
            DrawBox(handle,
                c + new Vector2(d.X * (b - arm), d.Y * b - w / 2),
                c + new Vector2(d.X * (b + w / 2), d.Y * b + w / 2),
                grow, color);
            DrawBox(handle,
                c + new Vector2(d.X * b - w / 2, d.Y * (b - arm)),
                c + new Vector2(d.X * b + w / 2, d.Y * b),
                grow, color);
        }

        DrawDot(handle, sight, color, grow);
    }

    private static void DrawGrid(DrawingHandleScreen handle, in Sight sight, Color color, float grow)
    {
        var c = sight.Center;
        var line = MathF.Max(1f, sight.Scale * 0.9f);
        var length = 26f * sight.Scale;
        var step = 6.5f * sight.Scale;

        foreach (var dir in Axes)
        {
            var side = new Vector2(-dir.Y, dir.X);
            DrawBox(handle,
                c + dir * sight.Gap - side * line / 2,
                c + dir * (sight.Gap + length) + side * line / 2,
                grow, color);

            for (var k = 1; k <= 3; k++)
            {
                var mark = c + dir * (sight.Gap + k * step);
                var half = (k % 2 == 1 ? 2.6f : 1.6f) * sight.Scale;
                DrawBox(handle, mark - side * half - dir * line / 2, mark + side * half + dir * line / 2, grow, color);
            }
        }
    }

    private void DrawDashedRing(DrawingHandleScreen handle, in Sight sight, Color color, float grow)
    {
        var radius = MathF.Max(sight.Gap, sight.Thickness * 1.5f);
        var extend = grow / radius;
        for (var k = 0; k < 12; k++)
        {
            var from = MathHelper.DegreesToRadians(k * 30f - 8f);
            DrawArc(handle, sight.Center, radius, from - extend, from + MathHelper.DegreesToRadians(16f) + extend,
                sight.Thickness * 0.7f + grow * 2, color);
        }

        DrawDot(handle, sight, color, grow);
    }

    private void DrawDiagonalArcs(DrawingHandleScreen handle, in Sight sight, Color color, float grow)
    {
        var radius = sight.Gap + sight.Thickness;
        var half = MathHelper.DegreesToRadians(22f) + grow / radius;
        for (var k = 0; k < 4; k++)
        {
            var middle = MathHelper.DegreesToRadians(45f + k * 90f);
            DrawArc(handle, sight.Center, radius, middle - half, middle + half, sight.Thickness * 0.85f + grow * 2, color);
        }

        DrawDot(handle, sight, color, grow);
    }

    private static void DrawCloud(DrawingHandleScreen handle, in Sight sight, Color color, float grow)
    {
        if (grow == 0f)
        {
            var radius = sight.Gap * 1.12f + 4f * sight.Scale;
            var layer = color.WithAlpha(color.A * 0.05f);
            for (var i = 0; i < CloudLayers; i++)
            {
                handle.DrawCircle(sight.Center, radius * (1f - (float) i / CloudLayers), layer);
            }

            handle.DrawCircle(sight.Center, MathF.Max(sight.Gap, sight.Thickness), color.WithAlpha(color.A * 0.35f), false);
        }

        DrawDot(handle, sight, color, grow);
    }

    private static void DrawRails(DrawingHandleScreen handle, in Sight sight, Color color, float grow)
    {
        var c = sight.Center;
        var half = RailLength * sight.Scale / 2;
        var g = sight.Gap;
        var t = sight.Thickness;

        DrawBox(handle, c + new Vector2(-half, g), c + new Vector2(half, g + t), grow, color);
        DrawBox(handle, c + new Vector2(-half, -g - t), c + new Vector2(half, -g), grow, color);
        DrawDot(handle, sight, color, grow);
    }

    private void DrawPixelCross(DrawingHandleScreen handle, in Sight sight, Color color, float grow)
    {
        var size = MathF.Max(2f, MathF.Round(2.5f * sight.Scale, MidpointRounding.AwayFromZero));
        var gap = Math.Max(2, (int) MathF.Round(sight.Gap / size));
        var corner = new Vector2(MathF.Round(sight.Center.X), MathF.Round(sight.Center.Y)) - new Vector2(MathF.Floor(size / 2));

        _cells[0] = Vector2i.Zero;
        for (var k = 0; k < PixelArm; k++)
        {
            _cells[1 + k * 4] = new Vector2i(gap + k, 0);
            _cells[2 + k * 4] = new Vector2i(-gap - k, 0);
            _cells[3 + k * 4] = new Vector2i(0, gap + k);
            _cells[4 + k * 4] = new Vector2i(0, -gap - k);
        }

        if (grow == 0f)
        {
            foreach (var cell in _cells)
            {
                DrawCell(handle, corner, size, cell, color);
            }

            return;
        }

        var count = 0;
        foreach (var cell in _cells)
        {
            foreach (var offset in PixelNeighbours)
            {
                var neighbour = cell + offset;
                if (Array.IndexOf(_cells, neighbour) >= 0 || Array.IndexOf(_outlineCells, neighbour, 0, count) >= 0)
                    continue;

                _outlineCells[count++] = neighbour;
                DrawCell(handle, corner, size, neighbour, color);
            }
        }
    }

    private static void DrawCell(DrawingHandleScreen handle, Vector2 corner, float size, Vector2i cell, Color color)
    {
        var topLeft = corner + (Vector2) cell * size;
        handle.DrawRect(new UIBox2(topLeft, topLeft + new Vector2(size)), color);
    }

    private static void DrawDot(DrawingHandleScreen handle, in Sight sight, Color color, float grow)
    {
        var half = new Vector2(sight.Thickness * 0.45f);
        DrawBox(handle, sight.Center - half, sight.Center + half, grow, color);
    }

    private static void DrawBox(DrawingHandleScreen handle, Vector2 a, Vector2 b, float grow, Color color)
    {
        handle.DrawRect(new UIBox2(Vector2.Min(a, b) - new Vector2(grow), Vector2.Max(a, b) + new Vector2(grow)), color);
    }

    private void DrawArc(DrawingHandleScreen handle, Vector2 center, float radius, float from, float to, float width, Color color)
    {
        var segments = Math.Clamp((int) (MathF.Abs(to - from) * radius / 4f), 3, MaxArcSegments);
        var inner = radius - width / 2;
        var outer = radius + width / 2;
        for (var i = 0; i <= segments; i++)
        {
            var angle = from + (to - from) * i / segments;
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            _vertices[i * 2] = center + dir * inner;
            _vertices[i * 2 + 1] = center + dir * outer;
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, new ReadOnlySpan<Vector2>(_vertices, 0, (segments + 1) * 2), color);
    }

    private void UpdateCursor(Control? viewport)
    {
        if (_hiddenCursorViewport == viewport)
            return;

        if (_hiddenCursorViewport != null)
            _hiddenCursorViewport.CustomCursorShape = null;

        _hiddenCursorViewport = viewport;
        if (viewport == null)
            return;

        _blankCursor ??= _clyde.CreateCursor(new SixLabors.ImageSharp.Image<Rgba32>(32, 32), Vector2i.Zero);
        viewport.CustomCursorShape = _blankCursor;
    }

    protected override void DisposeBehavior()
    {
        _cfg.UnsubValueChanged(CCVars.CrosshairColor, OnColorChanged);
        _cfg.UnsubValueChanged(CCVars.CrosshairScale, OnScaleChanged);
        _cfg.UnsubValueChanged(CCVars.CrosshairOutline, OnOutlineChanged);
        UpdateCursor(null);
        _blankCursor?.Dispose();
        base.DisposeBehavior();
    }

    private readonly record struct Sight(Vector2 Center, float Distance, float Angle, float Spread, float Scale)
    {
        public float Thickness => BaseThickness * Scale;
        public float TickLength => BaseTickLength * Scale;
        public float Gap => SpreadRadius + BaseMinGap * Scale;

        public float SpreadRadius
        {
            get
            {
                var real = MathF.Tan(Spread / 2) * Distance;
                var knee = SpreadKnee * Scale;
                if (real <= knee)
                    return real;

                var falloff = SpreadFalloff * Scale;
                return knee + falloff * MathF.Log(1 + (real - knee) / falloff);
            }
        }
    }
}
