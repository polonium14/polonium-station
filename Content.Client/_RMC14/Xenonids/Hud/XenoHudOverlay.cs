using System.Numerics;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared._RMC14.Xenonids.Rank;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Ghost;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Rounding;
using Content.Shared.StatusIcon;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using static Robust.Shared.Utility.SpriteSpecifier;

namespace Content.Client._RMC14.Xenonids.Hud;

public sealed class XenoHudOverlay : Overlay
{
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IResourceCache _resourceCache = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly ContainerSystem _container;
    private readonly DamageableSystem _damageable;
    private readonly MobStateSystem _mobState;
    private readonly MobThresholdSystem _mobThresholds;
    private readonly SpriteSystem _sprite;
    private readonly TransformSystem _transform;

    private readonly EntityQuery<DamageableComponent> _damageableQuery;
    private readonly EntityQuery<MobStateComponent> _mobStateQuery;
    private readonly EntityQuery<MobThresholdsComponent> _mobThresholdsQuery;
    private readonly EntityQuery<XenoPlasmaComponent> _xenoPlasmaQuery;
    private readonly EntityQuery<TransformComponent> _xformQuery;

    private readonly ShaderInstance _shader;
    private readonly ResPath _rsiPath = new("/Textures/_RMC14/Interface/xeno_hud.rsi");
    private static readonly ProtoId<HealthIconPrototype> DeadIconId = "HealthIconDead";

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public XenoHudOverlay()
    {
        IoCManager.InjectDependencies(this);

        _container = _entity.System<ContainerSystem>();
        _damageable = _entity.System<DamageableSystem>();
        _mobState = _entity.System<MobStateSystem>();
        _mobThresholds = _entity.System<MobThresholdSystem>();
        _sprite = _entity.System<SpriteSystem>();
        _transform = _entity.System<TransformSystem>();

        _damageableQuery = _entity.GetEntityQuery<DamageableComponent>();
        _mobStateQuery = _entity.GetEntityQuery<MobStateComponent>();
        _mobThresholdsQuery = _entity.GetEntityQuery<MobThresholdsComponent>();
        _xenoPlasmaQuery = _entity.GetEntityQuery<XenoPlasmaComponent>();
        _xformQuery = _entity.GetEntityQuery<TransformComponent>();

        _shader = _prototype.Index<ShaderPrototype>("unshaded").Instance();
        ZIndex = 1;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var local = _players.LocalEntity;
        var isAdminGhost = _entity.TryGetComponent(local, out GhostComponent? ghost) && ghost.CanGhostInteract;
        var isXeno = _entity.HasComponent<XenoComponent>(local);

        if (!isXeno && !isAdminGhost)
            return;

        var handle = args.WorldHandle;
        var eyeRot = args.Viewport.Eye?.Rotation ?? default;

        var scaleMatrix = Matrix3x2.CreateScale(new Vector2(1, 1));
        var rotationMatrix = Matrix3Helpers.CreateRotation(-eyeRot);

        handle.UseShader(_shader);

        if (isXeno || isAdminGhost)
        {
            DrawBars(in args, scaleMatrix, rotationMatrix);
            if (isXeno)
            {
                DrawDeadIcon(in args, scaleMatrix, rotationMatrix);
                DrawRank(in args, scaleMatrix, rotationMatrix);
            }
        }

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawBars(in OverlayDrawArgs args, Matrix3x2 scaleMatrix, Matrix3x2 rotationMatrix)
    {
        var handle = args.WorldHandle;
        var xenos = _entity.AllEntityQueryEnumerator<XenoComponent, SpriteComponent, TransformComponent>();
        while (xenos.MoveNext(out var uid, out var xeno, out var sprite, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (_container.IsEntityOrParentInContainer(uid, xform: xform))
                continue;

            var bounds = sprite.Bounds;
            var worldPos = _transform.GetWorldPosition(xform, _xformQuery);

            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                continue;

            var worldMatrix = Matrix3x2.CreateTranslation(worldPos);
            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matrix = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
            handle.SetTransform(matrix);

            if (_mobStateQuery.TryComp(uid, out var mobState) &&
                _mobState.IsDead(uid, mobState))
            {
                continue;
            }

            UpdateHealth((uid, xeno, sprite, mobState), handle);
            UpdatePlasma((uid, xeno, sprite), handle);
        }
    }

    private void DrawRank(in OverlayDrawArgs args, Matrix3x2 scaleMatrix, Matrix3x2 rotationMatrix)
    {
        var handle = args.WorldHandle;
        var ranks = _entity.EntityQueryEnumerator<XenoRankComponent, SpriteComponent, TransformComponent>();
        while (ranks.MoveNext(out var uid, out var comp, out var sprite, out var xform))
        {
            if (comp.Rank is < 2 or > 6)
                continue;

            if (xform.MapID != args.MapId)
                continue;

            if (_container.IsEntityOrParentInContainer(uid, xform: xform))
                continue;

            var bounds = sprite.Bounds;
            var worldPos = _transform.GetWorldPosition(xform, _xformQuery);

            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                continue;

            var worldMatrix = Matrix3x2.CreateTranslation(worldPos);
            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matrix = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
            handle.SetTransform(matrix);

            var icon = new Rsi(_rsiPath, $"hudxenoupgrade{comp.Rank}");
            var texture = _sprite.GetFrame(icon, _timing.CurTime);

            var yOffset = (bounds.Height + sprite.Offset.Y) / 2f - (float) texture.Height / EyeManager.PixelsPerMeter * bounds.Height;
            var xOffset = (bounds.Width + sprite.Offset.X) / 2f - (float) texture.Width / EyeManager.PixelsPerMeter * bounds.Width;

            handle.DrawTexture(texture, new Vector2(xOffset, yOffset));
        }
    }

    private void DrawDeadIcon(in OverlayDrawArgs args, Matrix3x2 scaleMatrix, Matrix3x2 rotationMatrix)
    {
        if (!_prototype.TryIndex(DeadIconId, out HealthIconPrototype? deadProto))
            return;

        var icon = deadProto.Icon;
        var handle = args.WorldHandle;
        var dead = _entity.AllEntityQueryEnumerator<MobStateComponent, SpriteComponent, TransformComponent>();
        while (dead.MoveNext(out var uid, out var comp, out var sprite, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (comp.CurrentState != MobState.Dead)
                continue;

            if (_container.IsEntityOrParentInContainer(uid, xform: xform))
                continue;

            var bounds = sprite.Bounds;
            var worldPos = _transform.GetWorldPosition(xform, _xformQuery);

            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                continue;

            var worldMatrix = Matrix3x2.CreateTranslation(worldPos);
            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matrix = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
            handle.SetTransform(matrix);

            var texture = _sprite.GetFrame(icon, _timing.CurTime);

            var yOffset = (bounds.Height + sprite.Offset.Y) / 2f - (float) texture.Height / EyeManager.PixelsPerMeter * bounds.Height;
            var xOffset = (bounds.Width + sprite.Offset.X) / 2f - (float) texture.Width / EyeManager.PixelsPerMeter * bounds.Width;

            handle.DrawTexture(texture, new Vector2(xOffset, yOffset));
        }
    }

    private void UpdateHealth(Entity<XenoComponent, SpriteComponent, MobStateComponent?> ent, DrawingHandleWorld handle)
    {
        var (uid, xeno, sprite, mobState) = ent;
        if (!_damageableQuery.TryComp(uid, out var damageable))
            return;

        var damage = _damageable.GetTotalDamage((uid, damageable));

        FixedPoint2? critThresholdNullable = null;
        FixedPoint2? deadThresholdNullable = null;
        if (_mobThresholdsQuery.TryComp(uid, out var mobThresholds))
        {
            _mobThresholds.TryGetThresholdForState(uid, MobState.Critical, out critThresholdNullable, mobThresholds);
            _mobThresholds.TryGetDeadThreshold(uid, out deadThresholdNullable, mobThresholds);
        }

        string state;
        if (_mobState.IsCritical(uid, mobState) ||
            _mobState.IsAlive(uid) &&
            critThresholdNullable != null &&
            damage > critThresholdNullable)
        {
            if (critThresholdNullable is not { } critThreshold || deadThresholdNullable is not { } deadThreshold)
                return;

            deadThreshold -= critThreshold;
            damage -= critThreshold;
            var level = ContentHelpers.RoundToLevels(damage.Double(), deadThreshold.Double(), 11);
            var name = level > 0 ? $"{level * 10}" : "1";
            state = $"xenohealth-{name}";
        }
        else
        {
            critThresholdNullable ??= deadThresholdNullable;
            if (critThresholdNullable == null)
                return;

            var level = ContentHelpers.RoundToLevels((critThresholdNullable - damage).Value.Double(), critThresholdNullable.Value.Double(), 11);
            var name = level > 0 ? $"{level * 10}" : "0";
            state = $"xenohealth{name}";
        }

        var icon = new Rsi(_rsiPath, state);
        var rsi = _resourceCache.GetResource<RSIResource>(icon.RsiPath).RSI;
        if (!rsi.TryGetState(icon.RsiState, out _))
            return;

        var texture = _sprite.GetFrame(icon, _timing.CurTime);

        var bounds = sprite.Bounds;
        var yOffset = (bounds.Height + sprite.Offset.Y) / 2f - (float) texture.Height / EyeManager.PixelsPerMeter * bounds.Height + xeno.HudOffset.Y;
        var xOffset = (bounds.Width + sprite.Offset.X) / 2f - (float) texture.Width / EyeManager.PixelsPerMeter * bounds.Width + xeno.HudOffset.X;

        handle.DrawTexture(texture, new Vector2(xOffset, yOffset));
    }

    private void UpdatePlasma(Entity<XenoComponent, SpriteComponent> ent, DrawingHandleWorld handle)
    {
        var (uid, xeno, sprite) = ent;
        if (!_xenoPlasmaQuery.TryComp(uid, out var comp) ||
            comp.MaxPlasma == 0)
        {
            return;
        }

        var plasma = comp.Plasma;
        var max = comp.MaxPlasma;
        var level = ContentHelpers.RoundToLevels(plasma.Double(), max, 11);
        var name = level > 0 ? $"{level * 10}" : "0";
        var state = $"plasma{name}";
        var icon = new Rsi(_rsiPath, state);
        var texture = _sprite.GetFrame(icon, _timing.CurTime);

        var bounds = sprite.Bounds;
        var yOffset = (bounds.Height + sprite.Offset.Y) / 2f - (float) texture.Height / EyeManager.PixelsPerMeter * bounds.Height + xeno.HudOffset.Y;
        var xOffset = (bounds.Width + sprite.Offset.X) / 2f - (float) texture.Width / EyeManager.PixelsPerMeter * bounds.Width + xeno.HudOffset.X;

        handle.DrawTexture(texture, new Vector2(xOffset, yOffset));
    }
}
