using Content.Shared._Polonium.Replicator;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client._Polonium.Replicator;

public sealed partial class ReplicatorRecallImmunityVisualsSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplicatorRecallImmuneComponent, ComponentStartup>(OnStartup);
        // ComponentShutdown is already subscribed by the shared system.
        SubscribeLocalEvent<ReplicatorRecallImmuneComponent, ComponentRemove>(OnRemove);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<ReplicatorRecallImmuneComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var immune, out var sprite))
        {
            var state = immune.EndTime - _timing.CurTime <= immune.FadeWarning ? immune.FadingState : immune.TraceState;
            var ent = (uid, sprite);

            if (_sprite.LayerMapTryGet(ent, ReplicatorRecallImmuneVisuals.Trace, out var layer, false)
                && _sprite.LayerGetRsiState(ent, layer) != state)
                _sprite.LayerSetRsiState(ent, layer, state);
        }
    }

    private void OnStartup(Entity<ReplicatorRecallImmuneComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var spriteEnt = (ent.Owner, sprite);
        var layer = _sprite.LayerMapReserve(spriteEnt, ReplicatorRecallImmuneVisuals.Trace);
        _sprite.LayerSetRsi(spriteEnt, layer, ent.Comp.TraceSprite, ent.Comp.TraceState);
        sprite.LayerSetShader(layer, "unshaded");
    }

    private void OnRemove(Entity<ReplicatorRecallImmuneComponent> ent, ref ComponentRemove args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprite.RemoveLayer((ent.Owner, sprite), ReplicatorRecallImmuneVisuals.Trace, false);
    }
}
