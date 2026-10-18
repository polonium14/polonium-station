using Content.Shared._Polonium.Replicator;
using Content.Shared.Lathe;
using Robust.Client.GameObjects;

namespace Content.Client._Polonium.Replicator;

public sealed class ReplicatorColossusFoundryVisualsSystem : VisualizerSystem<ReplicatorColossusFoundryComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, ReplicatorColossusFoundryComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var sprite = (uid, args.Sprite);
        if (AppearanceSystem.TryGetData<int>(uid, ReplicatorColossusFoundryVisuals.Stage, out var stage, args.Component))
            SpriteSystem.LayerSetRsiState(sprite, ReplicatorColossusFoundryVisualLayers.Stage, $"stage{stage}");

        AppearanceSystem.TryGetData<ReplicatorColossusFoundryState>(uid, ReplicatorColossusFoundryVisuals.State, out var state, args.Component);
        AppearanceSystem.TryGetData<bool>(uid, LatheVisuals.IsRunning, out var running, args.Component);

        var status = state switch
        {
            ReplicatorColossusFoundryState.Awaiting => "awaiting",
            ReplicatorColossusFoundryState.Complete => "complete",
            _ => running ? "working" : null,
        };

        if (!SpriteSystem.LayerMapTryGet(sprite, ReplicatorColossusFoundryVisualLayers.Status, out var layer, false))
            return;

        SpriteSystem.LayerSetVisible(sprite, layer, status != null);
        if (status != null)
            SpriteSystem.LayerSetRsiState(sprite, layer, status);
    }
}
