using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Mobs.Components;

namespace Content.Shared.Mobs.Systems;

/// <summary>
///     Adds and removes defined actions when a mob's <see cref="MobState"/> changes.
/// </summary>
public sealed partial class MobStateActionsSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private MobStateSystem _mobState = default!; // Polonium: soft crit

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<MobStateActionsComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<MobStateComponent, ComponentInit>(OnMobStateComponentInit);
    }

    private void OnMobStateChanged(EntityUid uid, MobStateActionsComponent component, MobStateChangedEvent args)
    {
        ComposeActions(uid, component, args.NewMobState);
    }

    private void OnMobStateComponentInit(EntityUid uid, MobStateComponent component, ComponentInit args)
    {
        if (!TryComp<MobStateActionsComponent>(uid, out var mobStateActionsComp))
            return;

        ComposeActions(uid, mobStateActionsComp, component.CurrentState);
    }

    /// <summary>
    /// Adds or removes actions from a mob based on mobstate.
    /// </summary>
    public void ComposeActions(EntityUid uid, MobStateActionsComponent component, MobState newMobState) // Polonium: public, soft crit recomposes on its threshold
    {
        if (!TryComp<ActionsComponent>(uid, out var action))
            return;

        foreach (var act in component.GrantedActions)
        {
            Del(act);
        }
        component.GrantedActions.Clear();

        // Polonium: succumb and the other crit actions are for hard crit only
        if (newMobState == MobState.Critical && _mobState.IsSoftCritical(uid))
            return;

        if (!component.Actions.TryGetValue(newMobState, out var toGrant))
            return;

        foreach (var id in toGrant)
        {
            EntityUid? act = null;
            if (_actions.AddAction(uid, ref act, id, uid, action))
                component.GrantedActions.Add(act.Value);
        }
    }
}
