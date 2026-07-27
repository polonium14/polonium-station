using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Events;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Rest;

public sealed partial class XenoRestSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoComponent, XenoRestActionEvent>(OnXenoRestAction);

        SubscribeLocalEvent<XenoRestingComponent, UpdateCanMoveEvent>(OnXenoRestingCanMove);
        SubscribeLocalEvent<XenoRestingComponent, AttackAttemptEvent>(OnXenoRestingAttackAttempt);
    }

    private void OnXenoRestingCanMove(Entity<XenoRestingComponent> xeno, ref UpdateCanMoveEvent args)
    {
        args.Cancel();
    }

    private void OnXenoRestAction(Entity<XenoComponent> xeno, ref XenoRestActionEvent args)
    {
        if (_timing.ApplyingState)
            return;

        var attempt = new XenoRestAttemptEvent();
        RaiseLocalEvent(xeno, ref attempt);

        if (attempt.Cancelled)
            return;

        args.Handled = true;

        if (HasComp<XenoRestingComponent>(xeno))
        {
            RemComp<XenoRestingComponent>(xeno);
            _appearance.SetData(xeno, XenoVisualLayers.Base, XenoRestState.NotResting);
            _actions.SetToggled((EntityUid)args.Action, false);
        }
        else
        {
            AddComp<XenoRestingComponent>(xeno);
            _appearance.SetData(xeno, XenoVisualLayers.Base, XenoRestState.Resting);
            _actions.SetToggled((EntityUid)args.Action, true);
        }

        _actionBlocker.UpdateCanMove(xeno);

        var ev = new XenoRestEvent(HasComp<XenoRestingComponent>(xeno));
        RaiseLocalEvent(xeno, ref ev);
    }

    private void OnXenoRestingAttackAttempt(Entity<XenoRestingComponent> xeno, ref AttackAttemptEvent args)
    {
        args.Cancel();
    }

    public bool IsResting(Entity<XenoRestingComponent?> ent)
    {
        return Resolve(ent, ref ent.Comp, false);
    }
}
