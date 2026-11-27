using System.Numerics;
using Content.Client.Graphics;
using Content.Shared._Polonium.WalkAnimation;
using Content.Shared.Buckle.Components;
using Content.Shared.Conveyor;
using Content.Shared.CCVar;
using Content.Shared.Gravity;
using Content.Shared.Hands.Components;
using Content.Shared.Humanoid;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Standing;
using Content.Shared.Throwing;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Utility;
using Robust.Shared.Configuration;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.WalkAnimation;

/// <summary>
/// Swings the arms and legs of walking humanoids. The sprite is drawn into its own render target anyway for post-shaders,
/// so a post-shader moves fixed limb regions of the finished image by one sprite pixel. That keeps clothes and gloves
/// on the limbs without knowing anything about the layers. Front and back: one leg and the opposite arm lift on every
/// step. Side: the legs spread forward and back, the nearer arm swings forward on every second step and the item in the
/// farther hand moves back. Tails, wings and held items are drawn by child entities outside the post-shader, as the
/// detached parts of the Aquila version: the regions would cut them, and held items move with their own hand.
/// </summary>
public sealed class WalkAnimationSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private StandingStateSystem _standing = default!;

    // has to be unique against ContentPostShaderIds
    private const string PostShaderId = "walk-animation";

    private static readonly ProtoId<ShaderPrototype> Shader = "PoloniumWalkAnimation";

    private static readonly WalkDetachedPart[] Parts = [WalkDetachedPart.Tail, WalkDetachedPart.RightHand, WalkDetachedPart.LeftHand];

    /// <summary>
    /// The limbs are moved before any effect that works on the silhouette (outlines, stealth, floor occlusion).
    /// </summary>
    private static readonly string[] RunBefore =
    [
        .. ContentPostShaderIds.BeforeOutlines,
        .. ContentPostShaderIds.AfterBaseEffects,
    ];

    /// <summary>
    /// Without movement for this long the current step is finished, so a stopped mob does not keep a limb raised.
    /// </summary>
    private static readonly TimeSpan SettleDelay = TimeSpan.FromSeconds(0.15);

    /// <summary>
    /// Without movement for this long the post-shader comes off and the sprite is drawn normally again.
    /// </summary>
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(1);

    /// <summary>
    /// A longer jump between frames is a teleport or a lag spike, not walking.
    /// </summary>
    private const float MaxFrameDistance = 1f;

    private const float MinFrameDistance = 0.0001f;

    private EntityQuery<BuckleComponent> _buckleQuery;
    private EntityQuery<ConveyedComponent> _conveyedQuery;
    private EntityQuery<HandsComponent> _handsQuery;
    private EntityQuery<PullableComponent> _pullableQuery;
    private EntityQuery<SpriteComponent> _spriteQuery;
    private EntityQuery<ThrownItemComponent> _thrownQuery;
    private EntityQuery<WalkAnimationComponent> _walkQuery;

    private readonly List<int> _indices = new();

    private bool _enabled;

    /// <summary>
    /// Offsets in sprite pixels (y up) of the limb regions and of the item in the farther hand, for one frame.
    /// </summary>
    private record struct LimbOffsets(Vector2 ArmA, Vector2 ArmB, Vector2 LegA, Vector2 LegB, Vector2 FarItem, Vector2 Body);

    public override void Initialize()
    {
        base.Initialize();

        _buckleQuery = GetEntityQuery<BuckleComponent>();
        _conveyedQuery = GetEntityQuery<ConveyedComponent>();
        _handsQuery = GetEntityQuery<HandsComponent>();
        _pullableQuery = GetEntityQuery<PullableComponent>();
        _spriteQuery = GetEntityQuery<SpriteComponent>();
        _thrownQuery = GetEntityQuery<ThrownItemComponent>();
        _walkQuery = GetEntityQuery<WalkAnimationComponent>();

        SubscribeLocalEvent<WalkAnimationComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<WalkAnimationComponent, ComponentShutdown>(OnWalkShutdown);
        SubscribeLocalEvent<WalkAnimationStateComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<WalkAnimationStateComponent, BeforePostShaderRenderEvent>(OnBeforePostShaderRender);

        Subs.CVar(_cfg, CCVars.WalkAnimationEnabled, OnEnabledChanged, true);
    }

    private void OnStartup(Entity<WalkAnimationComponent> ent, ref ComponentStartup args)
    {
        EnsureComp<WalkAnimationStateComponent>(ent);
    }

    private void OnWalkShutdown(Entity<WalkAnimationComponent> ent, ref ComponentShutdown args)
    {
        // FrameUpdate only sees entities with both components, so the state has to go with the marker.
        if (TryComp<WalkAnimationStateComponent>(ent, out var state))
        {
            Deactivate((ent, state));
            RemComp(ent, state);
        }
    }

    private void OnShutdown(Entity<WalkAnimationStateComponent> ent, ref ComponentShutdown args)
    {
        Deactivate(ent);
    }

    private void OnEnabledChanged(bool enabled)
    {
        _enabled = enabled;
        if (enabled)
            return;

        var query = EntityQueryEnumerator<WalkAnimationStateComponent>();
        while (query.MoveNext(out var uid, out var state))
        {
            Deactivate((uid, state));
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (!_enabled)
            return;

        var now = _timing.RealTime;
        // The copies are ordinary sprites shared by every viewport, so their offsets follow the main eye. Another viewport
        // with a differently rotated eye (a camera console) can show a held item a pixel off its hand.
        var eyeRotation = _eye.CurrentEye.Rotation;
        var query = EntityQueryEnumerator<WalkAnimationComponent, WalkAnimationStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var walk, out var state, out var xform))
        {
            var moved = 0f;
            if (xform.ParentUid == state.LastParent)
                moved = (xform.LocalPosition - state.LastPosition).Length();

            state.LastParent = xform.ParentUid;
            state.LastPosition = xform.LocalPosition;

            if (moved is > MinFrameDistance and <= MaxFrameDistance && CanWalk(uid))
            {
                state.Steps += moved / walk.StepLength;
                state.LastMove = now;
                Activate((uid, state));
            }
            else if (state.Active)
            {
                var idle = now - state.LastMove;
                if (idle > SettleDelay)
                    state.Steps = MathF.Ceiling(state.Steps);

                if (idle > IdleDelay)
                {
                    Deactivate((uid, state));
                    continue;
                }
            }

            if (state.Active)
                SyncDetached((uid, state), eyeRotation);
        }
    }

    private bool CanWalk(EntityUid uid)
    {
        if (_standing.IsDown(uid))
            return false;

        if (_buckleQuery.TryComp(uid, out var buckle) && buckle.Buckled)
            return false;

        if (_pullableQuery.TryComp(uid, out var pullable) && pullable.BeingPulled)
            return false;

        if ((MetaData(uid).Flags & MetaDataFlags.InContainer) != 0)
            return false;

        // Carried by something else, not walking.
        if (_thrownQuery.HasComp(uid) || _conveyedQuery.HasComp(uid))
            return false;

        return !_gravity.IsWeightless(uid);
    }

    private void Activate(Entity<WalkAnimationStateComponent> ent)
    {
        if (ent.Comp.Active)
            return;

        ent.Comp.Shader ??= _proto.Index(Shader).InstanceUnique();
        ent.Comp.Active = true;
        _sprite.SetPostShader(ent.Owner, new SpriteComponent.PostShaderArgs(PostShaderId, ent.Comp.Shader)
        {
            RaiseShaderEvent = true,
            Before = RunBefore,
        });
    }

    private void Deactivate(Entity<WalkAnimationStateComponent> ent)
    {
        if (!ent.Comp.Active)
            return;

        ent.Comp.Active = false;
        ent.Comp.Steps = MathF.Ceiling(ent.Comp.Steps);
        _sprite.RemovePostShader(ent.Owner, PostShaderId);

        foreach (var group in ent.Comp.Detached)
        {
            Attach(ent, group);
        }

        ent.Comp.Detached.Clear();
    }

    /// <summary>
    /// Direction the sprite is drawn in, the same way the renderer picks it.
    /// </summary>
    private static RsiDirection GetDirection(SpriteComponent sprite, Angle worldRotation, Angle eyeRotation)
    {
        return sprite.EnableDirectionOverride
            ? sprite.DirectionOverride.Convert(RsiDirectionType.Dir4)
            : SpriteComponent.Layer.GetDirection(RsiDirectionType.Dir4, (worldRotation + eyeRotation).Reduced().FlipPositive());
    }

    private static LimbOffsets GetOffsets(WalkAnimationComponent walk, float steps, RsiDirection direction)
    {
        var step = (int) MathF.Floor(steps);
        var raised = MathF.Round(MathF.Sin((steps - step) * MathF.PI));
        var phase = step % 2 == 0;
        var offsets = new LimbOffsets();

        if (walk.Bob)
            offsets.Body = new Vector2(0f, raised);

        if (direction is RsiDirection.East or RsiDirection.West)
        {
            var forward = direction == RsiDirection.East ? raised : -raised;
            offsets.LegA = new Vector2(-forward, 0f);
            offsets.LegB = new Vector2(forward, 0f);
            if (phase)
            {
                offsets.ArmA = new Vector2(forward, 0f);
                offsets.FarItem = new Vector2(-forward, 0f);
            }
        }
        else if (phase)
        {
            offsets.LegA = new Vector2(0f, raised);
            offsets.ArmB = new Vector2(0f, raised);
        }
        else
        {
            offsets.LegB = new Vector2(0f, raised);
            offsets.ArmA = new Vector2(0f, raised);
        }

        if (!walk.Arms)
        {
            offsets.ArmA = Vector2.Zero;
            offsets.ArmB = Vector2.Zero;
            offsets.FarItem = Vector2.Zero;
        }

        if (!walk.Legs)
        {
            offsets.LegA = Vector2.Zero;
            offsets.LegB = Vector2.Zero;
        }

        return offsets;
    }

    /// <summary>
    /// Offset of a detached part: the tail moves only with the body, a held item also with its hand.
    /// Front and side views show the right hand as "arm_a" (screen left from the front, nearer to the viewer from the side).
    /// </summary>
    private static Vector2 GetPartOffset(WalkDetachedPart part, RsiDirection direction, LimbOffsets offsets)
    {
        if (part == WalkDetachedPart.Tail)
            return offsets.Body;

        var side = direction is RsiDirection.East or RsiDirection.West;
        var rightIsA = direction is RsiDirection.South or RsiDirection.East;
        var isA = (part == WalkDetachedPart.RightHand) == rightIsA;
        if (isA)
            return offsets.Body + offsets.ArmA;

        return offsets.Body + (side ? offsets.FarItem : offsets.ArmB);
    }

    /// <summary>
    /// Mob sprite layers of a part, in drawing order. The tail part is the range from the tail layer up to the mask:
    /// the tail, the wings and their markings.
    /// </summary>
    private void GetPartLayers(Entity<SpriteComponent> sprite, WalkDetachedPart part, List<int> indices)
    {
        indices.Clear();
        if (part == WalkDetachedPart.Tail)
        {
            if (_sprite.LayerMapTryGet(sprite.AsNullable(), HumanoidVisualLayers.Tail, out var first, false)
                && _sprite.LayerMapTryGet(sprite.AsNullable(), "mask", out var end, false))
            {
                for (var i = first; i < end; i++)
                {
                    indices.Add(i);
                }
            }

            return;
        }

        var location = part == WalkDetachedPart.RightHand ? HandLocation.Right : HandLocation.Left;
        if (!_handsQuery.TryComp(sprite, out var hands))
            return;

        // RevealedLayers is the only record of which sprite layers draw the item in which hand. [Access] lets other
        // systems read the field but not call its methods, so it is only read here through the read-only interface;
        // the hands system stays the one that writes it.
        IReadOnlyDictionary<HandLocation, HashSet<string>> revealed = hands.RevealedLayers;
        if (!revealed.TryGetValue(location, out var keys))
            return;

        foreach (var key in keys)
        {
            if (_sprite.LayerMapTryGet(sprite.AsNullable(), key, out var index, false))
                indices.Add(index);
        }

        indices.Sort();
    }

    private bool AnyDrawn(Entity<SpriteComponent> sprite, List<int> indices)
    {
        foreach (var index in indices)
        {
            if (_sprite.TryGetLayer(sprite.AsNullable(), index, out var layer, false)
                && layer.Visible
                && (layer.Texture != null || layer.State.IsValid))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Moves the layers of a part to a child entity drawn right over the mob. The mob keeps the layers (other systems
    /// still own their visibility and colour), only transparent.
    /// </summary>
    private WalkDetachedGroup? Detach(Entity<SpriteComponent> sprite, WalkDetachedPart part, List<int> indices)
    {
        if (!AnyDrawn(sprite, indices))
            return null;

        var copy = Spawn(null, new EntityCoordinates(sprite, Vector2.Zero));
        var copySprite = EnsureComp<SpriteComponent>(copy);
        _sprite.CopySprite(sprite.AsNullable(), (copy, copySprite));
        _sprite.ClearPostShaders((copy, copySprite));
        var group = new WalkDetachedGroup(part, copy);

        // The chest stays in the copy as a transparent anchor layer that steers how the copy is sorted (see SyncGroup).
        if (!_sprite.LayerMapTryGet(sprite.AsNullable(), HumanoidVisualLayers.Chest, out var chest, false))
            chest = -1;

        for (var i = 0; _sprite.TryGetLayer((copy, copySprite), i, out var copyLayer, false); i++)
        {
            if (indices.Contains(i) && _sprite.TryGetLayer(sprite.AsNullable(), i, out var mobLayer, false))
            {
                group.Layers.Add(new WalkDetachedLayer(mobLayer, copyLayer, mobLayer.Color));
                _sprite.LayerSetColor(mobLayer, mobLayer.Color.WithAlpha(0f));
            }
            else if (i == chest)
            {
                _sprite.LayerSetColor(copyLayer, Color.Transparent);
                group.Anchor = copyLayer;
            }
            else
            {
                _sprite.LayerSetVisible(copyLayer, false);
            }
        }

        return group;
    }

    private void Attach(EntityUid mob, WalkDetachedGroup group)
    {
        // A dropped item or a swapped marking takes its layer off the mob, there is nothing to give back then.
        if (_spriteQuery.TryComp(mob, out var sprite))
        {
            for (var i = 0; _sprite.TryGetLayer((mob, sprite), i, out var mobLayer, false); i++)
            {
                foreach (var layer in group.Layers)
                {
                    if (layer.Mob == mobLayer && mobLayer.Color == layer.Color.WithAlpha(0f))
                        _sprite.LayerSetColor(mobLayer, layer.Color);
                }
            }
        }

        if (!TerminatingOrDeleted(group.Copy))
            QueueDel(group.Copy);
    }

    /// <summary>
    /// Keeps the detached parts in line with the mob: picked up or dropped items and layers swapped by other systems
    /// (a wagging tail swaps its marking) rebuild a part, colour and visibility changes are copied over.
    /// </summary>
    private void SyncDetached(Entity<WalkAnimationStateComponent> ent, Angle eyeRotation)
    {
        if (!_spriteQuery.TryComp(ent, out var sprite))
            return;

        // Stealth, floor occlusion and the holopad shade the whole silhouette. A copy would escape them, so while any of
        // them is on the sprite everything stays on the mob, cut by the limb regions.
        if (HasBaseEffect(sprite))
        {
            foreach (var stealthed in ent.Comp.Detached)
            {
                Attach(ent, stealthed);
            }

            ent.Comp.Detached.Clear();
            return;
        }

        var worldRotation = _transform.GetWorldRotation(ent.Owner);
        var direction = GetDirection(sprite, worldRotation, eyeRotation);
        if (!_walkQuery.TryComp(ent, out var walk))
            return;

        var offsets = GetOffsets(walk, ent.Comp.Steps, direction);

        foreach (var part in Parts)
        {
            GetPartLayers((ent, sprite), part, _indices);
            WalkDetachedGroup? group = null;
            foreach (var detached in ent.Comp.Detached)
            {
                if (detached.Part == part)
                    group = detached;
            }

            if (group != null && !SameLayers((ent, sprite), group, _indices))
            {
                Attach(ent, group);
                ent.Comp.Detached.Remove(group);
                group = null;
            }

            if (group == null)
            {
                group = Detach((ent, sprite), part, _indices);
                if (group == null)
                    continue;

                ent.Comp.Detached.Add(group);
            }

            SyncGroup((ent, sprite), group, GetPartOffset(part, direction, offsets), worldRotation, eyeRotation);
        }
    }

    private bool HasBaseEffect(SpriteComponent sprite)
    {
        foreach (var entry in _sprite.GetPostShaders(sprite))
        {
            if (Array.IndexOf(ContentPostShaderIds.AfterBaseEffects, entry.Id) >= 0)
                return true;
        }

        return false;
    }

    private bool SameLayers(Entity<SpriteComponent> sprite, WalkDetachedGroup group, List<int> indices)
    {
        if (indices.Count != group.Layers.Count)
            return false;

        for (var i = 0; i < indices.Count; i++)
        {
            if (!_sprite.TryGetLayer(sprite.AsNullable(), indices[i], out var layer, false) || layer != group.Layers[i].Mob)
                return false;
        }

        return true;
    }

    private void SyncGroup(Entity<SpriteComponent> sprite, WalkDetachedGroup group, Vector2 pixelOffset, Angle worldRotation, Angle eyeRotation)
    {
        if (!_spriteQuery.TryComp(group.Copy, out var copySprite))
            return;

        foreach (var layer in group.Layers)
        {
            // Someone else set the colour of the hidden layer: take it over and hide the layer again.
            if (layer.Mob.Color != layer.Color.WithAlpha(0f))
            {
                layer.Color = layer.Mob.Color;
                _sprite.LayerSetColor(layer.Mob, layer.Color.WithAlpha(0f));
            }

            _sprite.LayerSetColor(layer.Copy, layer.Color);
            _sprite.LayerSetVisible(layer.Copy, layer.Mob.Visible);
        }

        // The offset is given on screen; a rotating sprite applies its offsets in its own rotated space.
        // It moves the layers, not the whole copy, so it does not shift the bounds the renderer sorts by.
        var offset = pixelOffset / EyeManager.PixelsPerMeter;
        if (!sprite.Comp.NoRotation)
            offset = (-(worldRotation + eyeRotation)).RotateVec(offset);

        foreach (var layer in group.Layers)
        {
            _sprite.LayerSetOffset(layer.Copy, layer.Mob.Offset + offset);
        }

        // Sprites of one draw depth are y-sorted by the bottom of their bounds. With the copy's bottom a pixel below the
        // mob's the copy always comes right after it, whatever its layers or rounding do.
        // Sprite bounds include the sprite scale, layer bounds do not: the target bottom is turned into unscaled layer units.
        _sprite.SetScale((group.Copy, copySprite), sprite.Comp.Scale);
        var scaleY = MathF.Abs(sprite.Comp.Scale.Y);
        if (group.Anchor is { } anchor && scaleY > 0f)
        {
            var mobBottom = _sprite.GetLocalBounds(sprite).Bottom;
            var anchorBottom = _sprite.GetLocalBounds(anchor).Bottom - anchor.Offset.Y;
            var targetBottom = (mobBottom - 1f / EyeManager.PixelsPerMeter) / scaleY;
            _sprite.LayerSetOffset(anchor, new Vector2(0f, targetBottom - anchorBottom));
        }

        _sprite.SetOffset((group.Copy, copySprite), sprite.Comp.Offset);
        _sprite.SetRotation((group.Copy, copySprite), sprite.Comp.Rotation);
        copySprite.EnableDirectionOverride = sprite.Comp.EnableDirectionOverride;
        copySprite.DirectionOverride = sprite.Comp.DirectionOverride;
        copySprite.RenderOrder = sprite.Comp.RenderOrder;
        _sprite.SetVisible((group.Copy, copySprite), sprite.Comp.Visible);
        _sprite.SetColor((group.Copy, copySprite), sprite.Comp.Color);
        _sprite.SetDrawDepth((group.Copy, copySprite), sprite.Comp.DrawDepth);
    }

    private void OnBeforePostShaderRender(Entity<WalkAnimationStateComponent> ent, ref BeforePostShaderRenderEvent args)
    {
        if (args.Id != PostShaderId || args.Viewport.Eye is not { } eye)
            return;

        var sprite = args.Sprite;
        var shader = args.Shader;
        var (worldPos, worldRot) = _transform.GetWorldPositionRotation(ent.Owner);

        // Same sprite origin as the renderer uses for this sprite.
        if (sprite.Offset != Vector2.Zero)
            worldPos += sprite.NoRotation ? (-eye.Rotation).RotateVec(sprite.Offset) : worldRot.RotateVec(sprite.Offset);

        var direction = GetDirection(sprite, worldRot, eye.Rotation);
        var viewportSize = (Vector2) args.Viewport.Size;
        var origin = args.Viewport.WorldToLocal(worldPos);
        var worldToLocal = args.Viewport.GetWorldToLocalMatrix();
        var pixelsPerMeter = new Vector2(worldToLocal.M11, worldToLocal.M12).Length();
        var pixelScale = pixelsPerMeter / EyeManager.PixelsPerMeter * new Vector2(MathF.Abs(sprite.Scale.X), MathF.Abs(sprite.Scale.Y));

        shader.SetParameter("origin", new Vector2(origin.X, viewportSize.Y - origin.Y));
        shader.SetParameter("viewport_size", viewportSize);
        shader.SetParameter("pixel_scale", pixelScale);
        shader.SetParameter("facing", direction switch
        {
            RsiDirection.North => 1f,
            RsiDirection.East => 2f,
            RsiDirection.West => 3f,
            _ => 0f,
        });

        if (!_walkQuery.TryComp(ent, out var walk))
            return;

        var offsets = GetOffsets(walk, ent.Comp.Steps, direction);
        shader.SetParameter("leg_top", (float) walk.LegTop);
        shader.SetParameter("legs", walk.Legs ? 1f : 0f);
        shader.SetParameter("arms", walk.Arms ? 1f : 0f);
        shader.SetParameter("body", offsets.Body);
        shader.SetParameter("arm_a", offsets.ArmA);
        shader.SetParameter("arm_b", offsets.ArmB);
        shader.SetParameter("leg_a", offsets.LegA);
        shader.SetParameter("leg_b", offsets.LegB);
    }
}
