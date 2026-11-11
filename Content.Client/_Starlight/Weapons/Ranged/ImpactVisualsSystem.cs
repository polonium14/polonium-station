using Content.Client.DisplacementMap;
using Content.Shared._Starlight.CCVar;
using Content.Shared._Starlight.Effects;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Mobs.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Client._Starlight.Weapons.Ranged;

/// <summary>
/// Draws bullet holes, sparks and the wobble of the hit sprite where a shot lands.
/// </summary>
public sealed class ImpactVisualsSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private DisplacementMapSystem _displacement = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private static readonly EntProtoId ImpactProto = "ImpactEffect";
    private static readonly EntProtoId BulletHoleProto = "BulletHoleEffect";
    private static readonly EntProtoId SparksProto = "ImpactSparksEffect";
    private static readonly ProtoId<DisplacementEffect> DisplacementId = "ImpactDisplacement";

    // Polonium: Starlight marks metal with pierce levels, we go by the damage modifier set instead
    private static readonly HashSet<ProtoId<DamageModifierSetPrototype>> MetalModifierSets =
    [
        "PlastitaniumMod",
        "PlasteelStaticMod",
        "PlasteelMod",
        "SteelStaticMod",
        "SteelMod",
        "RodMetalMod",
        "BrassMod",
        "PreciousMetalMod",
        "Metallic",
        "StructuralMetallicStrong",
        "StructuralMetallic",
        "FlimsyMetallic",
        "StrongMetallic",
        "PerforatedMetallic",
    ];

    private const string ImpactLayer = "unshaded";
    private const float MaxHoleOffset = 0.5f;

    private bool _holesEnabled;
    private bool _sparksEnabled;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, StarlightCCVars.HolesEnabled, value => _holesEnabled = value, true);
        Subs.CVar(_cfg, StarlightCCVars.SparksEnabled, value => _sparksEnabled = value, true);
    }

    /// <summary>
    /// Shows a shot landing on <paramref name="target"/>. The angle is relative to the parent of the coordinates.
    /// </summary>
    public void PlayImpact(EntityCoordinates coords, Angle angle, EntityUid target)
    {
        if (!TryComp(coords.EntityId, out TransformComponent? relativeXform))
            return;

        if (TryComp<SpriteComponent>(target, out var targetSprite))
            RenderDisplacement(coords, angle, relativeXform, targetSprite);

        if (_holesEnabled && !HasComp<MobStateComponent>(target))
            Spawn(BulletHoleProto, coords.Offset(angle.ToVec() * _random.NextFloat(0f, MaxHoleOffset)));

        if (_sparksEnabled && IsMetal(target))
            Spawn(SparksProto, coords);
    }

    private void RenderDisplacement(EntityCoordinates coords, Angle angle, TransformComponent relativeXform, SpriteComponent targetSprite)
    {
        ISpriteLayer? source = null;
        foreach (var layer in targetSprite.AllLayers)
        {
            if ((layer.ActualRsi ?? layer.Rsi) == null || !layer.RsiState.IsValid)
                continue;

            source = layer;
            break;
        }

        if (source == null || source.PixelSize.X != 32 || source.PixelSize.Y != 32)
            return;

        var ent = Spawn(ImpactProto, coords);
        var sprite = Comp<SpriteComponent>(ent);

        var xform = Transform(ent);
        var targetWorldRot = angle + _xform.GetWorldRotation(relativeXform);
        var delta = targetWorldRot - _xform.GetWorldRotation(xform);
        _xform.SetLocalRotationNoLerp(ent, xform.LocalRotation + delta, xform);

        _sprite.LayerSetRsi((ent, sprite), ImpactLayer, source.ActualRsi ?? source.Rsi, source.RsiState);
        _sprite.LayerSetVisible((ent, sprite), ImpactLayer, true);
        _displacement.TryAddDisplacement(_proto.Index(DisplacementId).Displacement, (ent, sprite), 0, ImpactLayer, out _);
    }

    private bool IsMetal(EntityUid uid)
    {
        return TryComp<DamageableComponent>(uid, out var damageable)
               && damageable.DamageModifierSetId is { } modifierSet
               && MetalModifierSets.Contains(modifierSet);
    }
}
