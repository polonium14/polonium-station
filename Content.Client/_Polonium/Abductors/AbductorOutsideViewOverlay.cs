using System.Numerics;
using Content.Shared.Silicons.StationAi;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Abductors;

/// <summary>
/// An observation console looking away from the station: the same dim view as sleep,
/// with every tile outside camera coverage still hidden.
/// </summary>
public sealed partial class AbductorOutsideViewOverlay : Overlay
{
    private const float VisionUpdateRate = 1f / 30f;

    private static readonly ProtoId<ShaderPrototype> GreyscaleShader = "GreyscaleFullscreen";
    private static readonly ProtoId<ShaderPrototype> CircleShader = "CircleMask";
    private static readonly ProtoId<ShaderPrototype> StaticShader = "CameraStaticAccessible";

    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public EntityUid? OwnGrid;

    private readonly ShaderInstance _greyscale;
    private readonly ShaderInstance _circle;
    private readonly HashSet<(EntityUid Grid, Vector2i Tile)> _visible = new();
    private readonly HashSet<Vector2i> _gridVisible = new();
    private List<Entity<MapGridComponent>> _grids = new();
    private float _accumulator;

    public AbductorOutsideViewOverlay()
    {
        IoCManager.InjectDependencies(this);
        _greyscale = _proto.Index(GreyscaleShader).InstanceUnique();
        _circle = _proto.Index(CircleShader).InstanceUnique();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _entManager.TryGetComponent(_player.LocalEntity, out EyeComponent? eye) && args.Viewport.Eye == eye.Eye;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var handle = args.WorldHandle;
        var bounds = args.WorldBounds;

        _greyscale.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        handle.UseShader(_greyscale);
        handle.DrawRect(bounds, Color.White);

        handle.UseShader(_proto.Index(StaticShader).Instance());
        DrawHiddenTiles(args);

        handle.SetTransform(Matrix3x2.Identity);
        _circle.SetParameter("Zoom", args.Viewport.Eye?.Zoom.X ?? 1f);
        handle.UseShader(_circle);
        handle.DrawRect(bounds, Color.White);
        handle.UseShader(null);
    }

    private void DrawHiddenTiles(in OverlayDrawArgs args)
    {
        var maps = _entManager.System<SharedMapSystem>();
        var lookup = _entManager.System<EntityLookupSystem>();
        var transform = _entManager.System<SharedTransformSystem>();

        _grids.Clear();
        maps.FindGridsIntersecting(args.MapId, args.WorldAABB, ref _grids);

        _accumulator -= (float) _timing.FrameTime.TotalSeconds;
        if (_accumulator <= 0f)
        {
            _accumulator = VisionUpdateRate;
            RefreshVision(args.WorldBounds);
        }

        foreach (var grid in _grids)
        {
            if (grid.Owner == OwnGrid)
                continue;

            args.WorldHandle.SetTransform(transform.GetWorldMatrix(grid.Owner));
            var tiles = maps.GetTilesIntersecting(grid.Owner, grid.Comp, args.WorldBounds);
            while (tiles.MoveNext(out var tile))
            {
                if (!_visible.Contains((grid.Owner, tile.GridIndices)))
                    args.WorldHandle.DrawRect(lookup.GetLocalBounds(tile.GridIndices, grid.Comp.TileSize), Color.White);
            }
        }
    }

    private void RefreshVision(Box2Rotated bounds)
    {
        var vision = _entManager.System<StationAiVisionSystem>();

        _visible.Clear();
        foreach (var grid in _grids)
        {
            if (grid.Owner == OwnGrid || !_entManager.TryGetComponent(grid.Owner, out BroadphaseComponent? broadphase))
                continue;

            _gridVisible.Clear();
            vision.GetView((grid.Owner, broadphase, grid.Comp), bounds, _gridVisible);
            foreach (var tile in _gridVisible)
            {
                _visible.Add((grid.Owner, tile));
            }
        }
    }

    protected override void DisposeBehavior()
    {
        _greyscale.Dispose();
        _circle.Dispose();

        base.DisposeBehavior();
    }
}
