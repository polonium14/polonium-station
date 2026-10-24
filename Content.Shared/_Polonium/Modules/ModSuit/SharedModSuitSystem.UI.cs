using Content.Shared.Actions;
using Content.Shared._Polonium.Modules.Events;
using Content.Shared._Polonium.Modules.ModSuit.Components;
using Content.Shared._Polonium.Modules.ModSuit.Events;
using Content.Shared._Polonium.Modules.ModSuit.UI;
using JetBrains.Annotations;

namespace Content.Shared._Polonium.Modules.ModSuit;

public partial class SharedModSuitSystem
{
    [Dependency] private SharedActionsSystem _action = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    private void InitializeUI()
    {
        SubscribeLocalEvent<ModSuitUserInterfaceComponent, GetItemActionsEvent>(OnUiGetActions);
        SubscribeLocalEvent<ModSuitUserInterfaceComponent, ComponentRemove>(OnUiRemoved);
        SubscribeLocalEvent<ModSuitUserInterfaceComponent, ModSuitViewUiEvent>(OnUiViewUi);
        SubscribeLocalEvent<ModSuitUserInterfaceComponent, BoundUIOpenedEvent>(OnUiOpened);

        SubscribeLocalEvent<ModSuitUserInterfaceComponent, ModuleContainerModuleAddedEvent>((u, c, _) => UpdateUI((u, c)));
        SubscribeLocalEvent<ModSuitUserInterfaceComponent, ModuleContainerModuleRemovedEvent>((u, c, _) => UpdateUI((u, c)));
    }

    private void OnUiGetActions(Entity<ModSuitUserInterfaceComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.InHands)
            return;

        args.AddAction(ref ent.Comp.Action, ent.Comp.ActionId);
    }

    private void OnUiRemoved(Entity<ModSuitUserInterfaceComponent> ent, ref ComponentRemove args)
    {
        _action.RemoveAction(ent.Comp.Action);
    }

    private void OnUiViewUi(Entity<ModSuitUserInterfaceComponent> ent, ref ModSuitViewUiEvent args)
    {
        if (args.Handled)
            return;

        _ui.TryToggleUi(ent.Owner, ModSuitUiKey.Key, args.Performer);
        args.Handled = true;
    }

    private void OnUiOpened(Entity<ModSuitUserInterfaceComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUI(ent.AsNullable());
    }

    [PublicAPI]
    public void UpdateUI(Entity<ModSuitUserInterfaceComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return;

        if (!_ui.IsUiOpen(ent.Owner, ModSuitUiKey.Key))
            return;

        var ev = new ModSuitGetUiEntriesEvent();
        RaiseLocalEvent(ent.Owner, ev);

        var state = new ModSuitBoundUserInterfaceState(ev.Entries);
        _ui.SetUiState(ent.Owner, ModSuitUiKey.Key, state);
    }
}
