using System.Numerics;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Pheromones;
using Content.Shared.Ghost;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using static Robust.Shared.Utility.SpriteSpecifier;

namespace Content.Client._RMC14.Xenonids.Pheromones;

public sealed partial class XenoPheromonesOverlay : Overlay
{
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly SpriteSystem _sprite;
    private readonly TransformSystem _transform;
    private readonly EntityQuery<TransformComponent> _xformQuery;
    private readonly ShaderInstance _shader;

    private static readonly ResPath HudRsi = new("/Textures/_RMC14/Interface/xeno_pheromones_hud.rsi");
    private static readonly ProtoId<ShaderPrototype> UnshadedShader = "unshaded";

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public XenoPheromonesOverlay()
    {
        IoCManager.InjectDependencies(this);

        _sprite = _entity.System<SpriteSystem>();
        _transform = _entity.System<TransformSystem>();
        _xformQuery = _entity.GetEntityQuery<TransformComponent>();
        _shader = _prototype.Index(UnshadedShader).Instance();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var local = _players.LocalEntity;
        var isAdminGhost = _entity.TryGetComponent(local, out GhostComponent? ghost) && ghost.CanGhostInteract;
        if (!_entity.HasComponent<XenoComponent>(local) && !isAdminGhost)
            return;

        var handle = args.WorldHandle;
        var eyeRot = args.Viewport.Eye?.Rotation ?? default;
        var scaleMatrix = Matrix3x2.CreateScale(new Vector2(1, 1));
        var rotationMatrix = Matrix3Helpers.CreateRotation(-eyeRot);

        handle.UseShader(_shader);

        var recovery = _entity.AllEntityQueryEnumerator<XenoRecoveryPheromonesComponent, SpriteComponent, TransformComponent>();
        while (recovery.MoveNext(out var uid, out var comp, out var sprite, out var xform))
            DrawIcon((uid, sprite, xform), in args, comp.Icon, scaleMatrix, rotationMatrix);

        var warding = _entity.AllEntityQueryEnumerator<XenoWardingPheromonesComponent, SpriteComponent, TransformComponent>();
        while (warding.MoveNext(out var uid, out var comp, out var sprite, out var xform))
            DrawIcon((uid, sprite, xform), in args, comp.Icon, scaleMatrix, rotationMatrix);

        var frenzy = _entity.AllEntityQueryEnumerator<XenoFrenzyPheromonesComponent, SpriteComponent, TransformComponent>();
        while (frenzy.MoveNext(out var uid, out var comp, out var sprite, out var xform))
            DrawIcon((uid, sprite, xform), in args, comp.Icon, scaleMatrix, rotationMatrix);

        var sources = _entity.AllEntityQueryEnumerator<XenoActivePheromonesComponent, SpriteComponent, TransformComponent>();
        while (sources.MoveNext(out var uid, out var pheromones, out var sprite, out var xform))
        {
            XenoPheromones emitting = pheromones.Pheromones;
            var name = emitting switch
            {
                XenoPheromones.Recovery => "aura_recovery",
                XenoPheromones.Warding => "aura_warding",
                XenoPheromones.Frenzy => "aura_frenzy",
                _ => "aura_recovery",
            };
            DrawIcon((uid, sprite, xform), in args, new Rsi(HudRsi, name), scaleMatrix, rotationMatrix);
        }

        handle.UseShader(null);
    }

    private void DrawIcon(
        Entity<SpriteComponent, TransformComponent> ent,
        in OverlayDrawArgs args,
        SpriteSpecifier icon,
        Matrix3x2 scaleMatrix,
        Matrix3x2 rotationMatrix)
    {
        var (_, sprite, xform) = ent;
        if (xform.MapID != args.MapId)
            return;

        var bounds = sprite.Bounds;
        var worldPos = _transform.GetWorldPosition(xform, _xformQuery);

        if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
            return;

        var handle = args.WorldHandle;
        var worldMatrix = Matrix3x2.CreateTranslation(worldPos);
        var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
        var matrix = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
        handle.SetTransform(matrix);

        var texture = _sprite.GetFrame(icon, _timing.CurTime);
        var yOffset = (bounds.Height + sprite.Offset.Y) / 2f - (float)texture.Height / EyeManager.PixelsPerMeter * bounds.Height;
        var xOffset = (bounds.Width + sprite.Offset.X) / 2f - (float)texture.Width / EyeManager.PixelsPerMeter - 0.25f;

        handle.DrawTexture(texture, new Vector2(xOffset, yOffset));
    }
}
