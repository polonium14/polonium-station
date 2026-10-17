using Content.Shared.Actions;
using Content.Shared.StatusEffectNew;

namespace Content.Shared._Polonium.Burp;

public sealed partial class BurpStatusEffectSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;

    [SubscribeLocalEvent]
    private void OnApplied(Entity<BurpStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        _actions.AddAction(args.Target, ref ent.Comp.ActionEntity, ent.Comp.Action, ent.Owner);
    }

    [SubscribeLocalEvent]
    private void OnRemoved(Entity<BurpStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        _actions.RemoveAction(args.Target, ent.Comp.ActionEntity);
    }

    [SubscribeLocalEvent]
    private void OnBurp(Entity<BurpStatusEffectComponent> ent, ref BurpActionEvent args)
    {
        args.Handled = true;
    }
}
