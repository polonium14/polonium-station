using Content.Shared._RMC14.Xenonids;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Client.GameObjects;

namespace Content.Client._RMC14.Xenonids;

public sealed class XenoVisualizerSystem : VisualizerSystem<XenoStateVisualsComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, XenoStateVisualsComponent component, ref AppearanceChangeEvent args)
    {
        UpdateSprite(uid, args.Sprite, args.Component);
    }

    private void UpdateSprite(EntityUid uid, SpriteComponent? sprite, AppearanceComponent? appearance)
    {
        if (!Resolve(uid, ref sprite, ref appearance, false))
            return;

        if (!SpriteSystem.LayerMapTryGet((uid, sprite), XenoVisualLayers.Base, out var layer, false))
            return;

        var state = MobState.Alive;
        if (TryComp(uid, out MobStateComponent? mobState))
            state = mobState.CurrentState;

        var rsi = sprite.BaseRSI;
        if (rsi == null)
            return;

        if (AppearanceSystem.TryGetData(uid, RMCXenoStateVisuals.Dead, out bool dead, appearance) && dead ||
            state == MobState.Dead)
        {
            if (rsi.TryGetState("dead", out _))
                SpriteSystem.LayerSetRsiState((uid, sprite), layer, "dead");
            return;
        }

        if (AppearanceSystem.TryGetData(uid, RMCXenoStateVisuals.Downed, out bool downed, appearance) && downed ||
            state == MobState.Critical)
        {
            if (rsi.TryGetState("crit", out _))
                SpriteSystem.LayerSetRsiState((uid, sprite), layer, "crit");
            return;
        }

        if (AppearanceSystem.TryGetData(uid, RMCXenoStateVisuals.Resting, out bool resting, appearance) && resting ||
            AppearanceSystem.TryGetData(uid, XenoVisualLayers.Base, out XenoRestState restState, appearance) &&
            restState == XenoRestState.Resting)
        {
            if (rsi.TryGetState("sleeping", out _))
            {
                SpriteSystem.LayerSetRsiState((uid, sprite), layer, "sleeping");
                return;
            }
        }

        if (AppearanceSystem.TryGetData(uid, XenoVisualLayers.Fortify, out bool fortified, appearance) && fortified)
        {
            if (rsi.TryGetState("fortify", out _))
            {
                SpriteSystem.LayerSetRsiState((uid, sprite), layer, "fortify");
                return;
            }
        }

        if (AppearanceSystem.TryGetData(uid, XenoVisualLayers.Crest, out bool crested, appearance) && crested)
        {
            if (rsi.TryGetState("crest", out _))
            {
                SpriteSystem.LayerSetRsiState((uid, sprite), layer, "crest");
                return;
            }
        }

        if (rsi.TryGetState("alive", out _))
            SpriteSystem.LayerSetRsiState((uid, sprite), layer, "alive");
    }
}
