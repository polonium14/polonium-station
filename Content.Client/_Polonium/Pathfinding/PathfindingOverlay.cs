using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;
using Robust.Client.Player;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using System.Numerics;

namespace Content.Client._Polonium.Pathfinding;

public sealed class PathfindingOverlay : Overlay
{
    private readonly PlayerPathfindingSystem _sys;
    private readonly IEntityManager _entManager;
    private readonly SharedMapSystem _mapSystem;
    private readonly IPlayerManager _playerManager;
    private readonly SharedTransformSystem _transform;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly SpriteSpecifier _spriteStraight = new SpriteSpecifier.Rsi(new ResPath("/Textures/_Polonium/Pathfinding/path_rope.rsi"), "straight");
    private readonly SpriteSpecifier _spriteCorner = new SpriteSpecifier.Rsi(new ResPath("/Textures/_Polonium/Pathfinding/path_rope.rsi"), "corner");

    public PathfindingOverlay(
        PlayerPathfindingSystem sys,
        IEntityManager entManager,
        SharedMapSystem mapSystem,
        IPlayerManager playerManager,
        SharedTransformSystem transform)
    {
        _sys = sys;
        _entManager = entManager;
        _mapSystem = mapSystem;
        _playerManager = playerManager;
        _transform = transform;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var localPlayer = _playerManager.LocalEntity;

        if (localPlayer == null || !_entManager.TryGetComponent<PlayerPathfindingComponent>(localPlayer.Value, out var comp))
            return;

        if (comp.CurrentPath.Count < 2)
            return;

        if (!_entManager.TryGetComponent<TransformComponent>(localPlayer.Value, out var xform) || xform.GridUid == null)
            return;

        if (!_entManager.TryGetComponent<MapGridComponent>(xform.GridUid.Value, out var grid))
            return;

        var worldHandle = args.WorldHandle;
        var gridMatrix = _transform.GetWorldMatrix(xform.GridUid.Value);
        var localPlayerPos = _transform.GetRelativePosition(xform, xform.GridUid.Value);
        var path = comp.CurrentPath;
        var tileSize = grid.TileSize;

        worldHandle.SetTransform(gridMatrix);

        try
        {
            var spriteSystem = _entManager.System<Robust.Client.GameObjects.SpriteSystem>();

            var texStraight = spriteSystem.Frame0(_spriteStraight);
            var texCorner = spriteSystem.Frame0(_spriteCorner);

            var progress = PlayerPathfindingSystem.GetPathProgressIndex(path, localPlayerPos, tileSize);
            var loopStart = progress == 0 ? 1 : progress;

            for (var i = loopStart; i < path.Count; i++)
            {
                var tile = path[i];
                var pos = PlayerPathfindingSystem.TileCenter(tile, tileSize);

                Texture texture;
                Angle rotation;

                var prevTile = path[i - 1];
                var inDir = tile - prevTile;

                if (i == path.Count - 1)
                {
                    texture = texStraight;
                    rotation = new Angle(new Vector2(inDir.X, inDir.Y).ToAngle());
                }
                else
                {
                    var nextTile = path[i + 1];
                    var outDir = nextTile - tile;

                    if (inDir == outDir)
                    {
                        texture = texStraight;
                        rotation = new Angle(new Vector2(inDir.X, inDir.Y).ToAngle());
                    }
                    else
                    {
                        texture = texCorner;
                        if (inDir.X == 1 && outDir.Y == 1 || inDir.Y == -1 && outDir.X == -1)
                            rotation = Angle.FromDegrees(270);
                        else if (inDir.X == 1 && outDir.Y == -1 || inDir.Y == 1 && outDir.X == -1)
                            rotation = Angle.FromDegrees(0);
                        else if (inDir.X == -1 && outDir.Y == 1 || inDir.Y == -1 && outDir.X == 1)
                            rotation = Angle.FromDegrees(180);
                        else
                            rotation = Angle.FromDegrees(90);
                    }
                }

                DrawSegment(worldHandle, gridMatrix, texture, pos, tileSize, rotation);
            }
        }
        finally
        {
            worldHandle.SetTransform(Matrix3x2.Identity);
        }
    }

    private static void DrawSegment(
        DrawingHandleWorld worldHandle,
        Matrix3x2 gridMatrix,
        Texture texture,
        Vector2 pos,
        float tileSize,
        Angle rotation)
    {
        var localMatrix = Matrix3x2.CreateRotation((float) rotation.Theta) * Matrix3x2.CreateTranslation(pos);
        worldHandle.SetTransform(localMatrix * gridMatrix);

        var bounds = Box2.CenteredAround(Vector2.Zero, new Vector2(tileSize, tileSize));
        worldHandle.DrawTextureRect(texture, bounds);
        worldHandle.SetTransform(gridMatrix);
    }
}
