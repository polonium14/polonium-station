using Content.Shared.Sprite;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Network;

namespace Content.Shared._Polonium.Abductors;

public sealed partial class AbductorShrinkSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedScaleVisualsSystem _scale = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AbductorShrinkStatusEffectComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<AbductorShrinkStatusEffectComponent, StatusEffectRemovedEvent>(OnRemoved);
    }

    private void OnApplied(Entity<AbductorShrinkStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        if (_net.IsClient)
            return;

        ent.Comp.HadScaleVisuals = HasComp<ScaleVisualsComponent>(args.Target);
        ent.Comp.OriginalScale = _scale.GetSpriteScale(args.Target);
        _scale.SetSpriteScale(args.Target, ent.Comp.OriginalScale * ent.Comp.Scale);
    }

    private void OnRemoved(Entity<AbductorShrinkStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (_net.IsClient || TerminatingOrDeleted(args.Target))
            return;

        if (ent.Comp.HadScaleVisuals)
            _scale.SetSpriteScale(args.Target, ent.Comp.OriginalScale);
        else
            RemComp<ScaleVisualsComponent>(args.Target);
    }
}
