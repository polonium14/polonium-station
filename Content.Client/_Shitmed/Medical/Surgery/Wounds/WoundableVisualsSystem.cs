using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._Shitmed.Body;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.Body;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Client._Shitmed.Medical.Surgery.Wounds;

public sealed partial class WoundableVisualsSystem : VisualizerSystem<WoundableVisualsComponent>
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private WoundSystem _wound = default!;

    private const float AltBleedingSpriteChance = 0.15f;
    private const string BleedingSuffix = "Bleeding";
    private const string MinorSuffix = "Minor";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WoundableVisualsComponent, ComponentInit>(InitializeEntity, after: [typeof(WoundSystem)]);
        SubscribeLocalEvent<WoundableVisualsComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
        SubscribeLocalEvent<WoundableVisualsComponent, OrganGotInsertedEvent>(OnOrganGotInserted);
        SubscribeLocalEvent<WoundableVisualsComponent, OrganGotRemovedEvent>(OnOrganGotRemoved);
        SubscribeLocalEvent<WoundableComponent, AfterAutoHandleStateEvent>(OnWoundableState);
    }

    private void InitializeEntity(Entity<WoundableVisualsComponent> ent, ref ComponentInit args)
    {
        InitDamage(ent);
        InitBleeding(ent);
    }

    private void InitBleeding(Entity<WoundableVisualsComponent> ent)
    {
        if (ent.Comp.BleedingOverlay == null)
            return;
        AddDamageLayerToSprite(ent.Owner, ent.Comp.BleedingOverlay, BuildStateKey(ent.Comp.OccupiedLayer, MinorSuffix), BuildLayerKey(ent.Comp.OccupiedLayer, BleedingSuffix));
    }

    private void InitDamage(Entity<WoundableVisualsComponent> ent)
    {
        if (ent.Comp.DamageOverlayGroups is null)
            return;
        foreach (var (group, sprite) in ent.Comp.DamageOverlayGroups)
            AddDamageLayerToSprite(ent.Owner,
                sprite.Sprite,
                BuildStateKey(ent.Comp.OccupiedLayer, group, "100"),
                BuildLayerKey(ent.Comp.OccupiedLayer, group),
                sprite.Color);
    }

    private void OnAfterAutoHandleState(Entity<WoundableVisualsComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp(ent, out SpriteComponent? partSprite))
            return;

        UpdateWoundableVisuals(ent, (ent, partSprite));
    }

    private void OnOrganGotInserted(Entity<WoundableVisualsComponent> ent, ref OrganGotInsertedEvent args)
    {
        var bodyUid = args.Target;
        if (!HasComp<HumanoidProfileComponent>(bodyUid))
            return;

        if (ent.Comp.DamageOverlayGroups != null)
        {
            foreach (var (group, sprite) in ent.Comp.DamageOverlayGroups)
                if (!SpriteSystem.LayerMapTryGet(bodyUid, BuildLayerKey(ent.Comp.OccupiedLayer, group), out _, false))
                {
                    AddDamageLayerToSprite(bodyUid,
                        sprite.Sprite,
                        BuildStateKey(ent.Comp.OccupiedLayer, group, "100"),
                        BuildLayerKey(ent.Comp.OccupiedLayer, group),
                        sprite.Color);
                }
        }

        if (!SpriteSystem.LayerMapTryGet(bodyUid, BuildLayerKey(ent.Comp.OccupiedLayer, BleedingSuffix), out _, false)
            && ent.Comp.BleedingOverlay != null)
        {
            AddDamageLayerToSprite(bodyUid,
                ent.Comp.BleedingOverlay,
                BuildStateKey(ent.Comp.OccupiedLayer, MinorSuffix),
                BuildLayerKey(ent.Comp.OccupiedLayer, BleedingSuffix));
        }

        UpdateWoundableVisuals(ent, bodyUid);
    }

    private void OnOrganGotRemoved(Entity<WoundableVisualsComponent> ent, ref OrganGotRemovedEvent args)
    {
        RemoveWoundableLayers(args.Target, ent.Comp);

        if (TryComp(ent, out SpriteComponent? pieceSprite))
            UpdateWoundableVisuals(ent, (ent, pieceSprite));
    }

    private void OnWoundableState(Entity<WoundableComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp<WoundableVisualsComponent>(ent, out var visuals))
            return;

        var visualsEnt = (ent.Owner, visuals);

        if (!TryComp<OrganComponent>(ent, out var organ) || organ.Body is not { } bodyUid)
        {
            if (TryComp(ent, out SpriteComponent? partSprite))
                UpdateWoundableVisuals(visualsEnt, (ent.Owner, partSprite));

            return;
        }

        if (TryComp(bodyUid, out SpriteComponent? bodySprite))
            UpdateWoundableVisuals(visualsEnt, (bodyUid, bodySprite));
    }

    private void RemoveWoundableLayers(Entity<SpriteComponent?> ent, WoundableVisualsComponent visuals)
    {
        if (visuals.DamageOverlayGroups == null || !Resolve(ent, ref ent.Comp))
            return;

        foreach (var (group, _) in visuals.DamageOverlayGroups)
        {
            var layerKey = BuildLayerKey(visuals.OccupiedLayer, group);
            if (!SpriteSystem.LayerMapTryGet(ent, layerKey, out var layer, false))
                continue;
            SpriteSystem.LayerSetVisible(ent, layer, false);
            SpriteSystem.RemoveLayer(ent, layer);
            SpriteSystem.LayerMapRemove(ent, layerKey);
        }

        var bleedingKey = BuildLayerKey(visuals.OccupiedLayer, BleedingSuffix);
        if (!SpriteSystem.LayerMapTryGet(ent, bleedingKey, out var bleedLayer, false))
            return;
        SpriteSystem.LayerSetVisible(ent, bleedLayer, false);
        SpriteSystem.RemoveLayer(ent, bleedLayer, out _, false);
        SpriteSystem.LayerMapRemove(ent, bleedingKey, out _);
    }

    private void AddDamageLayerToSprite(Entity<SpriteComponent?> ent,
        string sprite,
        string state,
        string mapKey,
        string? color = null)
    {
        if (!Resolve(ent, ref ent.Comp) || SpriteSystem.LayerMapTryGet(ent, mapKey, out _, false)) // prevent dupes
            return;

        var newLayer = SpriteSystem.AddLayer(ent,
            new SpriteSpecifier.Rsi(
                new ResPath(sprite),
                state
            ));
        SpriteSystem.LayerMapSet(ent, mapKey, newLayer);
        if (color != null)
            SpriteSystem.LayerSetColor(ent, newLayer, Color.FromHex(color));
        SpriteSystem.LayerSetVisible(ent, newLayer, false);
    }

    private void UpdateWoundableVisuals(Entity<WoundableVisualsComponent> visuals, Entity<SpriteComponent?> sprite)
    {
        UpdateDamageVisuals(visuals, sprite);
        UpdateBleedingVisuals(visuals, sprite);
    }

    private void UpdateDamageVisuals(Entity<WoundableVisualsComponent> visuals, Entity<SpriteComponent?> sprite)
    {
        if (visuals.Comp.DamageOverlayGroups == null)
            return;
        foreach (var group in visuals.Comp.DamageOverlayGroups)
        {
            if (!SpriteSystem.LayerMapTryGet(sprite, $"{visuals.Comp.OccupiedLayer}{group.Key}", out var damageLayer, false))
                continue;
            var severityPoint = _wound.GetWoundableSeverityPoint(visuals, damageGroup: group.Key);
            UpdateDamageLayerState(sprite,
                damageLayer,
                $"{visuals.Comp.OccupiedLayer}_{group.Key}",
                severityPoint <= visuals.Comp.Thresholds.FirstOrDefault() ? 0 : GetThreshold(severityPoint, visuals.Comp));
        }
    }

    private void UpdateBleedingVisuals(Entity<WoundableVisualsComponent> ent, Entity<SpriteComponent?> sprite)
    {
        if (!TryComp<OrganComponent>(ent, out var organ))
            return;

        if (ent.Comp.BleedingOverlay is null)
        {
            UpdateParentBleedingVisuals(ent, organ, sprite);
            return;
        }

        UpdateOwnBleedingVisuals(ent, sprite);
    }

    private void UpdateParentBleedingVisuals(
        Entity<WoundableVisualsComponent> woundable,
        OrganComponent organ,
        Entity<SpriteComponent?> sprite)
    {
        if (organ.Category is not { } category
            || !LimbTargetMap.TryGetParentCategory(category, out var parentCategory)
            || organ.Body is not { } bodyUid
            || !TryComp<BodyComponent>(bodyUid, out var bodyComp)
            || !LimbTargetMap.TryGetOrganByCategory(EntityManager, bodyComp, parentCategory, out var parentUid))
            return;

        var partKey = GetLimbBleedingKey(category);
        var layerKey = BuildLayerKey(partKey, BleedingSuffix);
        var hasWounds = TryGetWoundData(woundable.Owner, out var wounds);
        var hasParentWounds = TryGetWoundData(parentUid, out var parentWounds);

        if (!hasWounds && !hasParentWounds)
        {
            if (SpriteSystem.LayerMapTryGet(sprite, layerKey, out var layer, false))
                SpriteSystem.LayerSetVisible(sprite, layer, false);
            return;
        }

        var totalBleeds = FixedPoint2.Zero;
        if (hasWounds)
            totalBleeds += CalculateTotalBleeding(wounds);
        if (hasParentWounds)
            totalBleeds += CalculateTotalBleeding(parentWounds);

        if (!SpriteSystem.LayerMapTryGet(sprite, layerKey, out var bleedingLayer, false))
            return;

        var threshold = CalculateBleedingThreshold(totalBleeds, woundable.Comp);
        UpdateBleedingLayerState(sprite, bleedingLayer, partKey, totalBleeds, threshold);
    }

    private void UpdateOwnBleedingVisuals(Entity<WoundableVisualsComponent> woundable, Entity<SpriteComponent?> sprite)
    {
        var layerKey = BuildLayerKey(woundable.Comp.OccupiedLayer, BleedingSuffix);

        if (!TryGetWoundData(woundable.Owner, out var wounds))
        {
            if (SpriteSystem.LayerMapTryGet(sprite, layerKey, out var layer, false))
                SpriteSystem.LayerSetVisible(sprite, layer, false);
            return;
        }

        var totalBleeds = CalculateTotalBleeding(wounds);
        if (!SpriteSystem.LayerMapTryGet(sprite, layerKey, out var bleedingLayer, false))
            return;
        var threshold = CalculateBleedingThreshold(totalBleeds, woundable.Comp);
        UpdateBleedingLayerState(sprite, bleedingLayer, woundable.Comp.OccupiedLayer.ToString(), totalBleeds, threshold);
    }

    private void SetLayerVisible(Entity<SpriteComponent?> sprite, int layer, bool visibility)
    {
        if (SpriteSystem.TryGetLayer(sprite, layer, out var layerData, false) && layerData.Visible != visibility)
            SpriteSystem.LayerSetVisible(sprite, layer, visibility);
    }

    private bool TryGetWoundData(Entity<AppearanceComponent?> entity, [NotNullWhen(true)] out WoundVisualizerGroupData? wounds)
    {
        wounds = null;
        if (!Resolve(entity, ref entity.Comp, false) || !AppearanceSystem.TryGetData(entity.Owner, WoundableVisualizerKeys.Wounds, out wounds, entity.Comp))
            return false;
        if (wounds.GroupList.Count != 0)
            return true;
        wounds = null;
        return false;
    }

    private FixedPoint2 CalculateTotalBleeding(params WoundVisualizerGroupData?[] woundGroups)
    {
        var total = FixedPoint2.Zero;

        foreach (var group in woundGroups)
        {
            if (group == null || group.GroupList.Count == 0)
                continue;

            foreach (var wound in group.GroupList.Select(GetEntity))
            {
                if (TryComp<BleedInflicterComponent>(wound, out var bleeds))
                    total += bleeds.BleedingAmount;
            }
        }

        return total;
    }

    private static BleedingSeverity CalculateBleedingThreshold(FixedPoint2 bleeding, WoundableVisualsComponent comp)
    {
        var nearestSeverity = BleedingSeverity.Minor;

        foreach (var (severity, value) in comp.BleedingThresholds.OrderByDescending(kv => kv.Value))
        {
            if (bleeding < value)
                continue;
            nearestSeverity = severity;
            break;
        }

        return nearestSeverity;
    }

    private static FixedPoint2 GetThreshold(FixedPoint2 threshold, WoundableVisualsComponent comp)
    {
        var nearestSeverity = FixedPoint2.Zero;

        foreach (var value in comp.Thresholds.OrderByDescending(kv => kv.Value))
        {
            if (threshold < value)
                continue;

            nearestSeverity = value;
            break;
        }

        return nearestSeverity;
    }

    private void UpdateBleedingLayerState(Entity<SpriteComponent?> sprite,
        int spriteLayer,
        string statePrefix,
        FixedPoint2 damage,
        BleedingSeverity threshold)
    {
        if (!Resolve(sprite, ref sprite.Comp))
            return;

        if (damage <= 0)
        {
            SetLayerVisible(sprite, spriteLayer, false);
            return;
        }

        SetLayerVisible(sprite, spriteLayer, true);

        var rsi = SpriteSystem.LayerGetEffectiveRsi(sprite, spriteLayer);
        if (rsi == null)
            return;
        var state = $"{statePrefix}_{threshold}";
        var altState = $"{state}_alt";

        if (_random.Prob(AltBleedingSpriteChance) && rsi.TryGetState(altState, out _))
            SpriteSystem.LayerSetRsiState(sprite, spriteLayer, altState);
        else if (rsi.TryGetState(state, out _))
            SpriteSystem.LayerSetRsiState(sprite, spriteLayer, state);
    }

    private void UpdateDamageLayerState(Entity<SpriteComponent?> sprite,
        int spriteLayer,
        string statePrefix,
        FixedPoint2 threshold)
    {
        if (threshold <= 0)
            SpriteSystem.LayerSetVisible(sprite, spriteLayer, false);
        else
        {
            if (!SpriteSystem.TryGetLayer(sprite, spriteLayer, out var layer, false) || !layer.Visible)
                SpriteSystem.LayerSetVisible(sprite, spriteLayer, true);
            SpriteSystem.LayerSetRsiState(sprite, spriteLayer, $"{statePrefix}_{threshold}");
        }
    }

    private static string GetLimbBleedingKey(ProtoId<OrganCategoryPrototype> category)
    {
        var id = category.Id;
        var symmetry = id.EndsWith("Left") ? "L" : "R";
        var partType = id.StartsWith("Hand") ? "Arm" : "Leg";
        return $"{symmetry}{partType}";
    }

    private static string BuildLayerKey(Enum baseLayer, string suffix) => $"{baseLayer}{suffix}";
    private static string BuildLayerKey(string baseLayer, string suffix) => $"{baseLayer}{suffix}";
    private static string BuildStateKey(Enum baseLayer, string suffix) => $"{baseLayer}_{suffix}";
    private static string BuildStateKey(Enum baseLayer, string group, string suffix) => $"{baseLayer}_{group}_{suffix}";
}
