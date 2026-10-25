using Content.Shared.Actions;
using Content.Shared.Vehicle;

namespace Content.Shared._Polonium.Vehicles;

public sealed partial class VehicleActionsSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private ActionContainerSystem _actionContainer = default!;

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<VehicleActionsComponent> ent, ref MapInitEvent args)
    {
        foreach (var proto in ent.Comp.Actions)
        {
            EntityUid? action = null;
            if (_actionContainer.EnsureAction(ent, ref action, proto))
                ent.Comp.ActionEntities.Add(action.Value);
        }

        Dirty(ent);
    }

    [SubscribeLocalEvent]
    private void OnOperatorSet(Entity<VehicleActionsComponent> ent, ref VehicleOperatorSetEvent args)
    {
        if (args.OldOperator is { } oldOperator)
            _actions.RemoveProvidedActions(oldOperator, ent);

        if (args.NewOperator is not { } newOperator)
            return;

        foreach (var action in ent.Comp.ActionEntities)
        {
            _actions.AddAction(newOperator, action, ent.Owner);
        }
    }

    [SubscribeLocalEvent]
    private void OnSignal(Entity<VehicleActionsComponent> ent, ref VehicleSignalActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
    }
}
